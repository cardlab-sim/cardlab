import type { InputHTMLAttributes } from 'react';
import styles from './Field.module.css';

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

function FieldError({ id, error }: { id: string; error: string | undefined }) {
  if (!error) {
    return null;
  }
  return (
    <p id={`${id}-error`} role="alert" className={styles['error']}>
      {error}
    </p>
  );
}

/** Labelled text input with inline error (§4.1). */
export function TextField({
  id,
  label,
  error,
  className,
  'aria-describedby': callerDescribedBy,
  ...rest
}: TextFieldProps) {
  const describedBy =
    [callerDescribedBy, error ? `${id}-error` : undefined].filter(Boolean).join(' ') || undefined;
  return (
    <div className={styles['field']}>
      <label htmlFor={id} className={styles['label']}>
        {label}
      </label>
      <input
        {...rest}
        id={id}
        className={[styles['control'], className].filter(Boolean).join(' ')}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
      />
      <FieldError id={id} error={error} />
    </div>
  );
}

/** Labelled native `<select>` with inline error (§4.1). */
export function SelectField({
  id,
  label,
  value,
  options,
  onChange,
  error,
  disabled,
}: SelectFieldProps) {
  return (
    <div className={styles['field']}>
      <label htmlFor={id} className={styles['label']}>
        {label}
      </label>
      <select
        id={id}
        className={styles['control']}
        value={value}
        disabled={disabled}
        onChange={(event) => {
          onChange(event.target.value);
        }}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      <FieldError id={id} error={error} />
    </div>
  );
}
