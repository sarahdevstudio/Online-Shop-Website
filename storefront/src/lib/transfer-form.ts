export interface TransferState {
  saved: boolean;
  error: string | null;
  errors: Record<string, string>;
  values: { tracking: string; time: string; cardLast4: string };
}

export const INITIAL_TRANSFER_STATE: TransferState = {
  saved: false,
  error: null,
  errors: {},
  values: { tracking: '', time: '', cardLast4: '' },
};
