import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PipelineConfigPage } from '../pipeline-config-page';

const CONFIG_TEXT =
  'LogLevel = 3\nLogToFile = true\nDatabaseType = 0\nDatabaseUrl = "sqlite://{ROOT_DIRECTORY}/db/signalnine.db"\n[Pipeline.Tasks.Probe]\nEnabled = true\nOverwriteExisting = false\n[Pipeline.Tasks.Preview]\nEnabled = true\nPreviewCount = 5\n';

const SCHEMA = {
  type: 'object',
  properties: {
    Pipeline: {
      type: 'object',
      title: 'Media pipeline',
      'x-signalnine-ui': {
        section: 'pipeline',
        sectionTitle: 'Media pipeline',
        order: 500,
      },
      properties: {
        Tasks: {
          type: 'object',
          properties: {
            Probe: {
              type: 'object',
              title: 'Probe',
              properties: {
                Enabled: {
                  type: 'boolean',
                  title: 'Probe task enabled',
                  default: true,
                  'x-signalnine-ui': { order: 100 },
                },
                OverwriteExisting: {
                  type: 'boolean',
                  title: 'Overwrite existing probe',
                  default: false,
                  'x-signalnine-ui': { order: 110 },
                },
              },
            },
            Preview: {
              type: 'object',
              title: 'Preview',
              properties: {
                Enabled: {
                  type: 'boolean',
                  title: 'Preview task enabled',
                  default: true,
                  'x-signalnine-ui': { order: 100 },
                },
                PreviewCount: {
                  type: 'integer',
                  title: 'Preview count',
                  default: 5,
                  minimum: 1,
                  maximum: 20,
                  'x-signalnine-ui': { order: 110 },
                },
              },
            },
          },
        },
      },
    },
  },
};

describe('PipelineConfigPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('renders pipeline tasks and saves TOML edits', async () => {
    let savedBody = '';
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input) === '/api/config/schema') return Response.json(SCHEMA);
      if (String(input) === '/api/config' && init?.method === 'POST') {
        savedBody = String(init.body);
        return new Response('', { status: 200 });
      }
      if (String(input) === '/api/config') {
        return new Response(CONFIG_TEXT, {
          status: 200,
          headers: { 'Content-Type': 'text/plain' },
        });
      }
      return new Response('not found', { status: 404 });
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Media pipeline' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Probe' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Preview' })).toBeInTheDocument();
    expect(screen.getByLabelText(/Probe task enabled/i)).toBeChecked();

    await userEvent.clear(screen.getByLabelText(/Preview count/i));
    await userEvent.type(screen.getByLabelText(/Preview count/i), '8');
    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(savedBody).toMatch(/PreviewCount = 8/));
    expect(savedBody).toMatch(/\[Pipeline.Tasks.Preview\]/);
  });
});

function renderPage() {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return render(
    <QueryClientProvider client={client}>
      <PipelineConfigPage />
    </QueryClientProvider>,
  );
}
