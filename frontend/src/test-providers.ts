/**
 * Test providers for the Angular unit test environment.
 *
 * These providers are added to the TestModule via the `providersFile` option
 * in angular.json. They run AFTER the init-testbed.js virtual file which
 * conditionally adds provideZoneChangeDetection() when zone.js is loaded.
 *
 * By providing provideZonelessChangeDetection() here, we ensure that all tests
 * use signal-based (zoneless) change detection, even when zone.js is loaded
 * for fakeAsync() support. Angular's DI deduplication resolves conflicts in
 * favour of the last provided CD strategy.
 *
 * This preserves the pre-existing test behavior (998 tests written for
 * zoneless CD) while still allowing fakeAsync() to work via zone.js/testing.
 */

import { provideZonelessChangeDetection, EnvironmentProviders } from '@angular/core';

const providers: EnvironmentProviders[] = [provideZonelessChangeDetection()];

export default providers;
