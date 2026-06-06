/**
 * Universal state shape for every store key.
 * All fields are optional so the initial empty state ({}) is always valid.
 */
export interface ResourceState<T> {
  isLoading?: boolean;
  data?: T;
  status?: 'Success' | 'Error' | 'Idle';
  errors?: { code: string; message: string }[];
}
