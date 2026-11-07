"""Structured AI review findings: post, triage, and the ai-blockers status. Stdlib + gh CLI only; no model.

The money reviewer returns JSON (no write tools). This script is the only thing that writes to GitHub:

  post    read FINDINGS (the reviewer's structured_output), post one inline review comment per new finding
          with a hidden machine-readable marker, save findings.json, then set the ai-blockers status.
  triage  a human replied on a finding thread with /fixed, /dismiss <reason> or /bug: verify, record the
          decision as a reply with a hidden marker, resolve the thread when the decision lifts it, re-set status.
  status  recompute ai-blockers: failure while any S1 finding has no fixed/dismissed decision,
          whatever the thread's resolved state.
  thread  a thread was resolved by hand: an S1 without a decision is reopened.

Markers (what the metrics collector reads; no prose parsing):
  <!-- finding {"id": ..., "key": ..., "severity": "S1", "path": ..., "line": ..., "requirement_ids": [...], ...} -->
  <!-- decision {"finding": ..., "outcome": "fixed|dismissed|escalated", "by": ..., "reason": ..., "issue": ...} -->

Env: GH_TOKEN, REPO (owner/name), PR, HEAD_SHA; post: FINDINGS, RUN_URL; triage: COMMENT_BODY, COMMENT_USER,
COMMENT_ASSOC, IN_REPLY_TO.
"""
import hashlib
import json
import os
import re
import subprocess
import sys

SEV_WORD = {"S1": "BLOCKER", "S2": "MAJOR", "S3": "MINOR", "S4": "COSMETIC"}
MARK_RE = re.compile(r"<!-- (finding|decision|review-state) (\{.*?\}) -->", re.S)
HUMAN_ASSOC = {"OWNER", "MEMBER", "COLLABORATOR"}


def env(name, default=None):
    v = os.environ.get(name, default)
    if v is None:
        sys.exit(f"missing env {name}")
    return v


def gh(*args, stdin=None):
    r = subprocess.run(["gh", *args], input=stdin, capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError(f"gh {' '.join(args[:3])}...: {r.stderr.strip()}")
    return r.stdout


def gh_json(*args, stdin=None):
    out = gh(*args, stdin=stdin)
    return json.loads(out) if out.strip() else None


def finding_key(path, line, sev):
    """Same file, same severity, same area (20-line bucket) = the same finding raised again."""
    bucket = (line or 0) // 20
    return hashlib.sha1(f"{path}|{sev}|{bucket}".encode()).hexdigest()[:10]


def markers(body, kind):
    return [json.loads(m.group(2)) for m in MARK_RE.finditer(body or "") if m.group(1) == kind]


def threads(repo, pr):
    owner, name = repo.split("/")
    q = """query($o:String!,$n:String!,$pr:Int!){repository(owner:$o,name:$n){pullRequest(number:$pr){
      reviewThreads(first:100){nodes{id isResolved isOutdated comments(first:50){nodes{databaseId body author{login}}}}}}}}"""
    d = gh_json("api", "graphql", "-f", f"query={q}", "-F", f"o={owner}", "-F", f"n={name}", "-F", f"pr={pr}")
    out = []
    for t in d["data"]["repository"]["pullRequest"]["reviewThreads"]["nodes"]:
        cs = t["comments"]["nodes"]
        if not cs:
            continue
        f = markers(cs[0]["body"], "finding")
        if f:
            out.append({"id": t["id"], "resolved": t["isResolved"], "outdated": t["isOutdated"],
                        "root_id": cs[0]["databaseId"], "finding": f[0],
                        "decisions": [d for c in cs[1:] for d in markers(c["body"], "decision")]})
    return out


def set_status(repo, sha, pr):
    ts = threads(repo, pr)
    open_s1 = [t for t in ts if t["finding"].get("severity") == "S1"
               and not any(d.get("outcome") in ("fixed", "dismissed") for d in t["decisions"])]
    state = "failure" if open_s1 else "success"
    desc = (f"{len(open_s1)} open blocker(s): fix and reply /fixed, or /dismiss <reason>" if open_s1
            else f"no open blockers ({len(ts)} finding(s) on this PR)")
    gh("api", "-X", "POST", f"repos/{repo}/statuses/{sha}", "-f", f"state={state}",
       "-f", "context=ai-blockers", "-f", f"description={desc[:139]}",
       "-f", f"target_url=https://github.com/{repo}/pull/{pr}/files")
    print(f"ai-blockers: {state} ({desc})")


def review_state(repo, pr):
    """The PR-level comment holding the latest money review: which commit, which finding keys it raised."""
    for c in gh_json("api", f"repos/{repo}/issues/{pr}/comments", "--paginate") or []:
        m = markers(c["body"], "review-state")
        if m:
            return c["id"], m[0]
    return None, None


def save_review_state(repo, pr, sha, keys):
    cid, _ = review_state(repo, pr)
    state = {"sha": sha, "keys": sorted(keys)}
    body = (f"Money review state: last run on `{sha[:7]}` raised {len(keys)} finding(s). "
            f"Maintained by tools/findings.py; do not edit.\n\n<!-- review-state {json.dumps(state)} -->")
    if cid:
        gh("api", "-X", "PATCH", f"repos/{repo}/issues/comments/{cid}", "-f", f"body={body}")
    else:
        gh("api", "-X", "POST", f"repos/{repo}/issues/{pr}/comments", "-f", f"body={body}")


def cmd_post():
    repo, pr, sha = env("REPO"), int(env("PR")), env("HEAD_SHA")
    raw = os.environ.get("FINDINGS", "").strip()
    data = json.loads(raw) if raw else {"findings": []}
    existing = {finding_key(t["finding"].get("path"), t["finding"].get("line"), t["finding"].get("severity"))
                for t in threads(repo, pr)}
    posted, skipped, run_keys = [], 0, set()
    for i, f in enumerate(data.get("findings", []), 1):
        sev = f.get("severity", "S3")
        key = finding_key(f.get("path"), f.get("line"), sev)
        run_keys.add(key)
        if key in existing:
            skipped += 1
            continue
        rec = {"id": f"F{pr}-{sha[:7]}-{i}", "key": key, "severity": sev, "path": f.get("path"),
               "line": f.get("line"), "title": f.get("title"), "requirement_ids": f.get("requirement_ids", []),
               "suspected_layer": f.get("suspected_layer"), "reviewer": "money", "sha": sha,
               "run": os.environ.get("RUN_URL", "")}
        reqs = ", ".join(rec["requirement_ids"]) or "no requirement cited"
        body = (f"**[money] {SEV_WORD.get(sev, sev)} ({sev})** · {reqs} · suspected layer: {rec['suspected_layer']}\n\n"
                f"**{rec['title']}**\n\n{f.get('detail', '')}\n\n"
                f"Reply on this thread with one of:\n"
                f"- `/fixed` after you've pushed the fix (checked: the commented line must have changed)\n"
                f"- `/dismiss <reason>` if it's wrong or not worth fixing (the reason is recorded)\n"
                f"- `/bug` to log it as a bug for triage" + (" (a blocker still blocks until fixed or dismissed)" if sev == "S1" else "")
                + f"\n\n<!-- finding {json.dumps(rec)} -->")
        payload = {"body": body, "commit_id": sha, "path": rec["path"]}
        if isinstance(rec["line"], int) and rec["line"] > 0:
            payload.update(line=rec["line"], side="RIGHT")
        else:
            payload["subject_type"] = "file"
        try:
            gh("api", "-X", "POST", f"repos/{repo}/pulls/{pr}/comments", "--input", "-", stdin=json.dumps(payload))
        except RuntimeError as e:  # line not in the diff: fall back to a file-level comment
            print(f"inline post failed ({e}); posting as file-level comment")
            payload.pop("line", None); payload.pop("side", None); payload["subject_type"] = "file"
            gh("api", "-X", "POST", f"repos/{repo}/pulls/{pr}/comments", "--input", "-", stdin=json.dumps(payload))
        posted.append(rec)
    with open("findings.json", "w", encoding="utf-8") as fh:
        json.dump({"pr": pr, "sha": sha, "raw": data, "posted": posted, "skipped_duplicates": skipped}, fh, indent=2)
    save_review_state(repo, pr, sha, run_keys)
    print(f"posted {len(posted)} finding(s), skipped {skipped} duplicate(s); this run raised {len(run_keys)} key(s)")
    set_status(repo, sha, pr)


def reply(repo, pr, root_id, text, decision=None):
    body = text + (f"\n\n<!-- decision {json.dumps(decision)} -->" if decision else "")
    gh("api", "-X", "POST", f"repos/{repo}/pulls/{pr}/comments/{root_id}/replies", "-f", f"body={body}")


def resolve(thread_id):
    m = "mutation($t:ID!){resolveReviewThread(input:{threadId:$t}){thread{isResolved}}}"
    gh("api", "graphql", "-f", f"query={m}", "-F", f"t={thread_id}")


def cmd_triage():
    repo, pr, sha = env("REPO"), int(env("PR")), env("HEAD_SHA")
    body, user, assoc = env("COMMENT_BODY").strip(), env("COMMENT_USER"), env("COMMENT_ASSOC")
    root = os.environ.get("IN_REPLY_TO", "")
    m = re.match(r"^/(fixed|dismiss|bug)\b\s*(.*)", body, re.S)
    if not m or not root:
        print("not a triage command on a reply; ignoring"); return
    t = next((t for t in threads(repo, pr) if str(t["root_id"]) == str(root)), None)
    if t is None:
        print("reply is not on a finding thread; ignoring"); return
    f, cmd, rest = t["finding"], m.group(1), m.group(2).strip()
    if assoc not in HUMAN_ASSOC:
        reply(repo, pr, t["root_id"], f"@{user}: only maintainers can triage findings ({assoc})."); return
    if t["resolved"]:
        print("thread already resolved"); return
    base = {"finding": f["id"], "severity": f["severity"], "by": user, "sha": sha}
    if cmd == "fixed":
        if not t["outdated"]:
            reply(repo, pr, t["root_id"], f"@{user}: not recorded as fixed. The commented line hasn't changed since "
                  "the finding. Push the fix first, or reply `/dismiss <reason>`.")
            return
        _, st = review_state(repo, pr)
        if not st or st.get("sha") != sha:
            reply(repo, pr, t["root_id"], f"@{user}: the money review hasn't run on your latest commit yet "
                  f"(`{sha[:7]}`). Wait for it to finish, then reply `/fixed` again.")
            return
        if finding_key(f.get("path"), f.get("line"), f.get("severity")) in st.get("keys", []):
            reply(repo, pr, t["root_id"], f"@{user}: not recorded as fixed. The money review on `{sha[:7]}` raised "
                  "this finding again. Fix it, or reply `/dismiss <reason>`.")
            return
        reply(repo, pr, t["root_id"], f"Recorded: **fixed** by @{user}. Checked: the line changed, and the money "
              f"review on `{sha[:7]}` no longer raises it.", {**base, "outcome": "fixed"})
        resolve(t["id"])
    elif cmd == "dismiss":
        if not rest:
            reply(repo, pr, t["root_id"], f"@{user}: a dismissal needs a reason, e.g. `/dismiss intended: the rule allows one decimal here`.")
            return
        reply(repo, pr, t["root_id"], f"Recorded: **dismissed** by @{user}. Reason: {rest}",
              {**base, "outcome": "dismissed", "reason": rest})
        resolve(t["id"])
    else:  # bug
        for lbl, color in (("bug", "d73a4a"), ("found-by:ai-review", "5319e7"), (f"severity:{f['severity']}", "b60205"),
                           ("layer:needs-triage", "fbca04")):
            gh("label", "create", lbl, "--color", color, "--force", "-R", repo)
        url = gh("issue", "create", "-R", repo, "--title", f"[{f['severity']}] {f['title']} (found on PR #{pr})",
                 "--label", f"bug,found-by:ai-review,severity:{f['severity']},layer:needs-triage",
                 "--body", (f"Found by the money reviewer on #{pr} at `{f['path']}:{f.get('line')}`.\n\n"
                            f"Requirements: {', '.join(f.get('requirement_ids') or []) or 'none cited'}. "
                            f"Suspected layer: {f.get('suspected_layer')} (triage sets the real root-cause layer).\n\n"
                            f"Escalated by @{user}.\n\n<!-- finding {json.dumps(f)} -->")).strip()
        issue = url.rsplit("/", 1)[-1]
        still = f["severity"] == "S1"
        reply(repo, pr, t["root_id"], f"Recorded: **escalated** by @{user} as {url}."
              + (" This is a blocker, so it still blocks the merge until it's fixed (`/fixed`) or dismissed." if still else ""),
              {**base, "outcome": "escalated", "issue": int(issue) if issue.isdigit() else issue})
        if not still:
            resolve(t["id"])
    set_status(repo, sha, pr)


def cmd_thread():
    repo, pr, sha = env("REPO"), int(env("PR")), env("HEAD_SHA")
    node, user = env("THREAD_ID"), env("ACTOR")
    t = next((t for t in threads(repo, pr) if t["id"] == node), None)
    if t is None:
        print("not a finding thread"); return
    decided = any(d.get("outcome") in ("fixed", "dismissed") for d in t["decisions"])
    if t["finding"].get("severity") == "S1" and not decided:
        m = "mutation($t:ID!){unresolveReviewThread(input:{threadId:$t}){thread{isResolved}}}"
        gh("api", "graphql", "-f", f"query={m}", "-F", f"t={node}")
        reply(repo, pr, t["root_id"], f"@{user}: reopened. A blocker can't be resolved by hand. Reply `/fixed` "
              "after pushing the fix, or `/dismiss <reason>`.")
    elif not decided:
        reply(repo, pr, t["root_id"], f"Recorded: resolved by hand by @{user}, no reason given.",
              {"finding": t["finding"]["id"], "severity": t["finding"]["severity"], "by": user, "sha": sha,
               "outcome": "resolved-without-decision"})
    set_status(repo, sha, pr)


if __name__ == "__main__":
    cmds = {"post": cmd_post, "triage": cmd_triage, "thread": cmd_thread,
            "status": lambda: set_status(env("REPO"), env("HEAD_SHA"), int(env("PR")))}
    if len(sys.argv) != 2 or sys.argv[1] not in cmds:
        sys.exit(__doc__)
    cmds[sys.argv[1]]()
