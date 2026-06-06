/**
 * Domain port for querying the latest version of a plugin from the catalog.
 * Infrastructure adapters implement this interface.
 */

import { Observable } from 'rxjs';

export abstract class CatalogLatestVersionPort {
  abstract getLatestVersion(pluginName: string): Observable<string | null>;
}
