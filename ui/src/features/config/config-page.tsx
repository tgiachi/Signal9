import { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useConfig } from './use-config';
import { ConfigForm } from './config-form';
import { ConfigRawEditor } from './config-raw-editor';
import { safeParseToml } from '@/lib/toml';
import { readConfigSummary } from './config-summary';

export function ConfigPage() {
  const cfg = useConfig();
  const [tab, setTab] = useState<'form' | 'raw'>('form');
  const [rawText, setRawText] = useState<string | null>(null);

  useEffect(() => {
    if (cfg.lastSaveStatus === 'success') toast.success('Configuration saved & reloaded');
    if (cfg.lastSaveStatus === 'error' && cfg.lastError) {
      toast.error(cfg.lastError.message);
    }
  }, [cfg.lastSaveStatus, cfg.lastError]);

  if (cfg.isLoading) return <div className="p-4 text-fg-1">Loading configuration…</div>;
  if (cfg.isError)
    return <div className="p-4 text-accent-err">Failed to load configuration.</div>;

  const baseText = rawText ?? cfg.text ?? '';
  const rawIsValid = safeParseToml(baseText).ok;
  const summary = readConfigSummary(baseText);

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 p-3">
      <header className="grid gap-2 rounded-[6px] bg-bg-2 p-3 md:grid-cols-4">
        <SummaryItem label="Database" value={summary.databaseType} detail={summary.databaseUrl} />
        <SummaryItem
          label="Logging"
          value={summary.logLevel}
          detail={`file: ${summary.logToFile}`}
        />
        <SummaryItem label="JWT" value={summary.jwtIssuer} detail={summary.jwtExpiration} />
        <SummaryItem
          label="Jobs"
          value={`${summary.maxConcurrentJobs} concurrent`}
          detail={`${summary.maxLogEntriesPerJob} logs/job`}
        />
      </header>
      <Tabs
        value={tab}
        onValueChange={(v) => setTab(v as 'form' | 'raw')}
        className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-[6px] bg-bg-2"
      >
        <div className="flex items-center justify-between bg-bg-4 px-3 py-2">
          <TabsList className="h-8">
            <TabsTrigger value="form" disabled={!rawIsValid}>
              Form
            </TabsTrigger>
            <TabsTrigger value="raw">Raw TOML</TabsTrigger>
          </TabsList>
          <span
            className={
              'rounded-[3px] px-2 py-1 font-mono text-[10px] font-bold uppercase tracking-label ' +
              (summary.valid ? 'bg-accent-live text-bg-5' : 'bg-accent-err text-fg-0')
            }
          >
            TOML {summary.valid ? 'valid' : 'invalid'}
          </span>
        </div>
        <TabsContent value="form" className="m-0 min-h-0 flex-1">
          <ConfigForm
            key={baseText}
            initialText={baseText}
            isSaving={cfg.isSaving}
            onSave={(text) => {
              setRawText(text);
              void cfg.save(text);
            }}
          />
        </TabsContent>
        <TabsContent value="raw" className="m-0 min-h-0 flex-1">
          <ConfigRawEditor
            key={baseText}
            initialText={baseText}
            isSaving={cfg.isSaving}
            onSave={(text) => {
              setRawText(text);
              void cfg.save(text);
            }}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function SummaryItem({
  label,
  value,
  detail,
}: {
  label: string;
  value: string;
  detail: string;
}) {
  return (
    <div className="min-w-0 rounded-[6px] bg-bg-3 px-3 py-2">
      <div className="font-mono text-[10px] uppercase tracking-label text-fg-3">{label}</div>
      <div className="truncate text-sm font-semibold text-fg-0">{value}</div>
      <div className="truncate font-mono text-[11px] text-fg-3">{detail}</div>
    </div>
  );
}
