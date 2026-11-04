import type { InputHTMLAttributes } from 'react';
import { notImplemented } from '../notImplemented';

/** Props of TextField. */
export interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'id'> {
  /** Input id; also derives the error id `${id}-error`. */
  id: string;
  /** Visible label text (never placeholder-only). */
  label: string;
  /** Inline error; when set: rendered under the input with role="alert", input gets aria-invalid and aria-describedby. */
  error?: string | undefined;
}

/** Props of SelectField. */
export interface SelectFieldProps {
  id: string;
  label: string;
  value: string;
  options: readonly { value: string; label: string }[];
  onChange: (value: string) => void;
  error?: string | undefined;
  disabled?: boolean;
}

/** Labelled text input with inline error (§4.1). Implemented in PR-02 (with Field.module.css). */
export function TextField(props: TextFieldProps) {
  return notImplemented('PR-02', props);
}

/** Labelled native `<select>` with inline error (§4.1). Implemented in PR-02. */
export function SelectField(props: SelectFieldProps) {
  return notImplemented('PR-02', props);
}
