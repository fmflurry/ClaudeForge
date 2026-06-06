/**
 * @claudeforge/plugin-template — public API
 *
 * Re-exports all public types and functions from the generator module.
 */

export type {
  TemplateLanguage,
  PluginType,
  UseCaseTag,
  GeneratedFileMap,
  GeneratorOptions,
} from './generator.js';

export { generatePluginTemplate } from './generator.js';
