import type { ReactNode } from 'react';
import { notImplemented } from '../notImplemented';

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

/**
 * Semantic `<table>` (thead/th scope="col"/tbody) inside an `overflow-x: auto` container (§4.1 table rule).
 * Implemented in PR-02 (with DataTable.module.css).
 */
export function DataTable<T>(props: DataTableProps<T>) {
  return notImplemented('PR-02', props);
}
