// @ts-check
import eslint from '@eslint/js';
import tseslint from 'typescript-eslint';
import angular from 'angular-eslint';
import boundaries from 'eslint-plugin-boundaries';

/**
 * Architecture lives here, not in a document. Angular has no ArchUnit, so import
 * boundaries are the executable form of the layering rule:
 *
 *   core/      singletons, guards, interceptors, app-wide services
 *   shared/    dumb reusable components -- knows nothing about any feature
 *   features/  one folder per feature, no cross-feature imports
 *   __GENERATED__/  the generated API client: imported by everyone, edited by no one
 *
 * Feature-to-feature imports arrive one individually-reasonable line at a time and
 * are a week of untangling by the time they are obvious in a diff. Catching them in
 * `npm run verify` puts them inside the agent's definition of done.
 */
const GENERATED = '__GENERATED__';

export default tseslint.config(
  {
    ignores: [
      'dist/**',
      '.angular/**',
      'coverage/**',
      'node_modules/**',
      `${GENERATED}/**`, // generated code is type-checked, never linted or hand-fixed
    ],
  },
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      ...tseslint.configs.recommended,
      ...tseslint.configs.stylistic,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    plugins: { boundaries },
    settings: {
      // Patterns match folders, outermost first, so the layer folders must not nest
      // inside one another -- that is why the generated client sits beside core/
      // rather than inside it. Files under src/app that belong to no layer (main.ts,
      // app.ts, app.config.ts) are simply unclassified and unconstrained.
      'boundaries/elements': [
        { type: 'api', pattern: GENERATED },
        { type: 'core', pattern: 'src/app/core' },
        { type: 'shared', pattern: 'src/app/shared' },
        { type: 'feature', pattern: 'src/app/features/*', capture: ['featureName'] },
      ],
      'boundaries/include': ['src/**/*.ts'],
      // Without a resolver that knows about .ts, every import resolves to nothing and
      // every boundary rule silently passes. eslint-import-resolver-node ships with
      // the plugin; if you use TypeScript path aliases (@app/...), swap in
      // eslint-import-resolver-typescript so those resolve too.
      'import/resolver': { node: { extensions: ['.ts', '.js', '.json'] } },
    },
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'app', style: 'camelCase' },
      ],
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: 'app', style: 'kebab-case' },
      ],
      'boundaries/dependencies': [
        'error',
        {
          default: 'disallow',
          policies: [
            {
              from: [{ element: { type: 'core' } }],
              allow: [{ to: { element: { type: ['core', 'shared', 'api'] } } }],
            },
            {
              from: [{ element: { type: 'shared' } }],
              allow: [{ to: { element: { type: ['shared', 'api'] } } }],
            },
            {
              from: [{ element: { type: 'feature' } }],
              allow: [
                { to: { element: { type: ['core', 'shared', 'api'] } } },
                {
                  // a feature may import itself, and only itself
                  to: {
                    element: {
                      type: 'feature',
                      captured: {
                        featureName: '{{from.captured.featureName}}',
                      },
                    },
                  },
                },
              ],
            },
            {
              from: [{ element: { type: 'api' } }],
              allow: [{ to: { element: { type: 'api' } } }],
            },
          ],
        },
      ],
    },
  },
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
    rules: {},
  },
);
