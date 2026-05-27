import { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useConfig } from './use-config';
import { ConfigForm } from './config-form';
import { ConfigRawEditor } from './config-raw-editor';
import { safeParseToml } from '@/lib/toml';

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
    return <div className="p-4 text-error">Failed to load configuration.</div>;

  const baseText = rawText ?? cfg.text ?? '';
  const rawIsValid = safeParseToml(baseText).ok;

  return (
    <Tabs
      value={tab}
      onValueChange={(v) => setTab(v as 'form' | 'raw')}
      className="flex h-full flex-col"
    >
      <TabsList className="mx-3 mt-2 w-fit">
        <TabsTrigger value="form" disabled={!rawIsValid}>
          Form
        </TabsTrigger>
        <TabsTrigger value="raw">Raw TOML</TabsTrigger>
      </TabsList>
      <TabsContent value="form" className="min-h-0 flex-1">
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
      <TabsContent value="raw" className="min-h-0 flex-1">
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
  );
}
