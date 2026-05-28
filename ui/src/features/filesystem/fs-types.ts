export type FsEntry = {
  name: string;
  path: string;
  isDirectory: boolean;
};

export type FsBrowseResponse = {
  path: string;
  parent: string | null;
  entries: FsEntry[];
};
