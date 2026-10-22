import styles from './SpecsTable.module.css';

export interface SpecRow {
  label: string;
  value: string;
}

export function SpecsTable({ caption, rows }: { caption: string; rows: readonly SpecRow[] }) {
  if (rows.length === 0) return null;
  return (
    <table className={styles.table}>
      <caption className="ui-visually-hidden">{caption}</caption>
      <tbody>
        {rows.map((row) => (
          <tr key={row.label}>
            <th scope="row">{row.label}</th>
            <td>{row.value}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
