/**
 * InjectionToken for the browser dist folder path used during SSR
 * to read translation JSON files from disk (avoids relative HTTP requests
 * which have no host in an SSR context).
 */

import { InjectionToken } from '@angular/core';

export const I18N_DIST_PATH = new InjectionToken<string>('I18N_DIST_PATH');

/**
 * Injectable function type for reading a file from disk.
 * Abstracted so the spec can stub it without touching the filesystem.
 */
export type FileReader = (path: string) => string;

export const I18N_FILE_READER = new InjectionToken<FileReader>('I18N_FILE_READER');
