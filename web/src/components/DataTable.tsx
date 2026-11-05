import type { ReactNode } from 'react';
import styles from './DataTable.module.css';

/** One column of a DataTable. */
export interface Column<T> {
  /** Header text. */
  header: string;
  /** Cell content for a row. */
  cell: (row: T) => ReactNode;
  /** Right-align (amounts; tabular numerals). */
  numeric?: boolean;
}

/** Props of DataTable. */
export interface DataTableProps<T> {
  /** Visible `<caption>` (accessible name of the table). */
  caption: string;
  columns: readonly Column<T>[];
  rows: readonly T[];
  rowKey: (row: T) => string;
  /** Text shown in a single full-width row when `rows` is empty. */
  emptyText: string;
}

/** Semantic `<table>` (thead/th scope="col"/tbody) inside an `overflow-x: auto` container (§4.1 table rule). */
export function DataTable<T>({ caption, columns, rows, rowKey, emptyText }: DataTableProps<T>) {
  const cellClass = (numeric: boolean | undefined): string | undefined =>
    numeric ? styles['numeric'] : undefined;
  return (
    <div className={styles['scroll']}>
      <table className={styles['table']}>
        <caption className={styles['caption']}>{caption}</caption>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column.header} scope="col" className={cellClass(column.numeric)}>
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 ? (
            <tr>
              <td colSpan={columns.length} className={styles['empty']}>
                {emptyText}
              </td>
            </tr>
          ) : (
            rows.map((row) => (
              <tr key={rowKey(row)}>
                {columns.map((column) => (
                  <td key={column.header} className={cellClass(column.numeric)}>
                    {column.cell(row)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
