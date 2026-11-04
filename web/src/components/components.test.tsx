import { describe, test } from 'vitest';

describe('primitives', () => {
  // T-W-6 · PR-02 · web unit
  // Given TextField with an error, When rendered, Then the input is labelled by its <label>, has aria-invalid and aria-describedby pointing to an element with role="alert" holding the error; without error none of these
  // Catches: errors not announced or not tied to the field (§4.1, AC-37)
  test.todo('T-W-6 fields have visible labels and announced inline errors');

  // T-W-6 · PR-02 · web unit
  // Given Button pending, When rendered, Then disabled with aria-busy and the pending label; Badge renders its text; Notice error has role="alert", success role="status"; DataTable renders a caption, th scope=col and the empty text
  // Catches: colour-only status, toast-only feedback, inaccessible tables (§4.1)
  test.todo('T-W-6 button, badge, notice and table are accessible');
});
