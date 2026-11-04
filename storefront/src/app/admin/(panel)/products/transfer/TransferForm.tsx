'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { FileField, Form, FormActions, FormSection } from '@/components/form';
import { adminMessages } from '@/lib/admin/messages';
import { transferMessages as t } from '@/lib/admin/transfer-messages';
import { formatNumber } from '@/lib/format';
import styles from '../../../admin.module.css';

interface Summary {
  rows: number;
  productsToCreate: number;
  productsToUpdate: number;
  variantsToCreate: number;
  variantsToUpdate: number;
  unchanged: number;
}

interface Preview {
  canApply: boolean;
  summary: Summary;
  rows: { row: number; productSlug: string; sku: string; action: string }[];
  errors: { row: number; column: string; message: string }[];
  errorsTruncated: boolean;
}

type Outcome = { kind: 'preview'; preview: Preview } | { kind: 'applied'; summary: Summary } | { kind: 'failed'; message: string };

const MAX_BYTES = 2 * 1024 * 1024;

/**
 * Pick a file, preview (a dry run: nothing is written), then apply. The file stays in the input between the two steps and is sent
 * again; the server re-reads and re-checks it, so the preview is advice and the apply is the authority.
 */
export function TransferForm() {
  const router = useRouter();
  const [busy, setBusy] = useState<'preview' | 'apply' | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const [fileError, setFileError] = useState<string | undefined>();
  const [previewed, setPreviewed] = useState<File | null>(null);

  async function send(mode: 'preview' | 'apply', picked?: File) {
    // Apply sends the very file that was previewed, even if another one was picked since.
    const file = mode === 'apply' ? previewed : picked;
    if (!file) {
      setFileError(t.noFile);
      return;
    }
    if (file.size > MAX_BYTES) {
      setFileError(t.tooBig);
      return;
    }
    setFileError(undefined);
    setBusy(mode);
    try {
      const body = new FormData();
      body.set('file', file);
      const response = await fetch(`/admin/api/catalog/import?mode=${mode}`, { method: 'POST', body });
      const json = (await response.json().catch(() => null)) as { ok?: boolean; message?: string; result?: Preview | { summary: Summary } } | null;
      if (!response.ok || !json?.ok || !json.result) {
        setOutcome({ kind: 'failed', message: json?.message ?? t.failed });
        if (mode === 'apply') setPreviewed(null);
      } else if (mode === 'preview') {
        setOutcome({ kind: 'preview', preview: json.result as Preview });
        setPreviewed(file);
      } else {
        setOutcome({ kind: 'applied', summary: (json.result as { summary: Summary }).summary });
        setPreviewed(null);
        router.refresh();
      }
    } catch {
      setOutcome({ kind: 'failed', message: t.failed });
    } finally {
      setBusy(null);
    }
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void send('preview', (event.currentTarget.elements.namedItem('file') as HTMLInputElement | null)?.files?.[0]);
  }

  const preview = outcome?.kind === 'preview' ? outcome.preview : null;
  const canApply = preview?.canApply === true && previewed !== null;

  return (
    <section className={styles.panel} aria-labelledby="import">
      <h2 id="import" className="ui-heading-4">
        {t.importTitle}
      </h2>
      <Form onSubmit={onSubmit} errors={{ file: fileError }}>
        <FormSection>
          <FileField name="file" label={t.file} accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" hint={t.fileHint} error={fileError} />
          <FormActions>
            <Button type="submit" disabled={busy !== null}>
              {busy === 'preview' ? t.working : t.preview}
            </Button>
          </FormActions>
        </FormSection>
      </Form>
      <div aria-live="polite">
        {outcome?.kind === 'failed' && <Alert variant="danger">{outcome.message}</Alert>}
        {outcome?.kind === 'applied' && (
          <Alert variant="success" heading={t.applied}>
            <SummaryList summary={outcome.summary} />
          </Alert>
        )}
      </div>

      {preview && (
        <>
          <Alert variant={preview.canApply ? 'success' : 'danger'} heading={t.summary}>
            {preview.canApply ? t.ok : t.hasErrors}
            <SummaryList summary={preview.summary} />
          </Alert>

          {preview.errors.length > 0 && (
            <>
              <h3 className="ui-heading-5">{t.errors}</h3>
              <div className={styles.tableWrap} tabIndex={0} role="region" aria-label={`${adminMessages.scrollableTable}: ${t.errors}`}>
                <table className={styles.table}>
                  <thead>
                    <tr>
                      <th>{t.colRow}</th>
                      <th>{t.colColumn}</th>
                      <th>{t.colMessage}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {preview.errors.map((error, index) => (
                      <tr key={`${error.row}-${error.column}-${index}`}>
                        <td className={styles.num}>{error.row === 0 ? t.headerRow : formatNumber(error.row)}</td>
                        <td className={styles.ltr}>{error.column}</td>
                        <td>{error.message}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {preview.errorsTruncated && <p className="ui-hint">{t.errorsTruncated}</p>}
            </>
          )}

          {preview.rows.length > 0 && (
            <details>
              <summary>{t.rows}</summary>
              <div className={styles.tableWrap} tabIndex={0} role="region" aria-label={`${adminMessages.scrollableTable}: ${t.rows}`}>
                <table className={styles.table}>
                  <thead>
                    <tr>
                      <th>{t.colRow}</th>
                      <th>{t.colProduct}</th>
                      <th>{t.colSku}</th>
                      <th>{t.colAction}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {preview.rows.map((row) => (
                      <tr key={row.row}>
                        <td className={styles.num}>{formatNumber(row.row)}</td>
                        <td className={styles.ltr}>{row.productSlug}</td>
                        <td className={styles.ltr}>{row.sku}</td>
                        <td>{t.actionLabel(row.action)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </details>
          )}

          {canApply && (
            <Form onSubmit={(event) => { event.preventDefault(); void send('apply'); }}>
              <FormSection hint={t.applyHint}>
                <FormActions>
                  <Button type="submit" disabled={busy !== null}>
                    {busy === 'apply' ? t.working : t.apply}
                  </Button>
                </FormActions>
              </FormSection>
            </Form>
          )}
        </>
      )}
    </section>
  );
}

function SummaryList({ summary }: { summary: Summary }) {
  const items: [string, number][] = [
    [t.sRows, summary.rows],
    [t.sCreateProducts, summary.productsToCreate],
    [t.sUpdateProducts, summary.productsToUpdate],
    [t.sCreateVariants, summary.variantsToCreate],
    [t.sUpdateVariants, summary.variantsToUpdate],
    [t.sUnchanged, summary.unchanged],
  ];
  return (
    <ul>
      {items.map(([label, value]) => (
        <li key={label}>
          {label}: <strong>{formatNumber(value)}</strong>
        </li>
      ))}
    </ul>
  );
}
