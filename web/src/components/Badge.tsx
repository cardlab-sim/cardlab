import type { ReactNode } from 'react';
import { notImplemented } from '../notImplemented';

/** Badge tones map to the §4.1 status tokens (active, frozen, approved, declined) or neutral. */
export type BadgeTone = 'active' | 'frozen' | 'approved' | 'declined' | 'neutral';

/** Text status badge: the text carries the meaning, colour only reinforces it (§4.1 "never colour alone"). Implemented in PR-02. */
export function Badge(props: { tone: BadgeTone; children: ReactNode }) {
  return notImplemented('PR-02', props);
}
