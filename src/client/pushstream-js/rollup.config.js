import terser from '@rollup/plugin-terser';

const banner = `/*!
 * PushStream JavaScript Client v${process.env.npm_package_version || '0.1.0'}
 * A lightweight, zero-dependency SSE client library
 * (c) ${new Date().getFullYear()}
 * Released under the MIT License
 */`;

const baseConfig = {
  input: 'src/index.js',
  output: {
    banner,
    sourcemap: true
  }
};

export default [
  // UMD build for browsers (script tag)
  {
    ...baseConfig,
    output: {
      ...baseConfig.output,
      file: 'dist/pushstream.js',
      format: 'umd',
      name: 'PushStream',
      exports: 'named'
    }
  },

  // Minified UMD build for production
  {
    ...baseConfig,
    output: {
      ...baseConfig.output,
      file: 'dist/pushstream.min.js',
      format: 'umd',
      name: 'PushStream',
      exports: 'named',
      sourcemap: true
    },
    plugins: [
      terser({
        format: {
          comments: /^!/  // Keep banner comment
        }
      })
    ]
  },

  // ES Module build for modern bundlers
  {
    ...baseConfig,
    output: {
      ...baseConfig.output,
      file: 'dist/pushstream.esm.js',
      format: 'esm'
    }
  },

  // CommonJS build for Node.js
  {
    ...baseConfig,
    output: {
      ...baseConfig.output,
      file: 'dist/pushstream.cjs.js',
      format: 'cjs',
      exports: 'named'
    }
  }
];
