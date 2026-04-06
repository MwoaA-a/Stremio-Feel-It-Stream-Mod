// Feel It Stream - Standalone Bundle Webpack Config
// Builds a single IIFE bundle (feelit.bundle.js) that can be injected
// into any Stremio-web instance without modifying its source code.
//
// React and ReactDOM are bundled WITH the plugin because Stremio does not
// expose them as window globals. FIS creates independent React roots
// (via ReactDOM.createRoot) so having a separate React instance is safe.
//
// Usage:
//   npm run build       (production)
//   npm run dev         (development + watch)

const path = require('path');
const webpack = require('webpack');
const TerserPlugin = require('terser-webpack-plugin');

module.exports = (env, argv) => ({
    mode: argv.mode || 'production',
    devtool: argv.mode === 'production' ? false : 'eval-source-map',
    entry: './src/standalone/bootstrap.js',
    output: {
        path: path.join(__dirname, 'dist'),
        filename: 'feelit.bundle.js',
        iife: true,
        clean: true,
    },
    module: {
        rules: [
            {
                test: /\.js$/,
                exclude: /node_modules/,
                use: {
                    loader: 'babel-loader',
                    options: {
                        presets: [
                            '@babel/preset-env',
                            '@babel/preset-react',
                        ],
                    },
                },
            },
            {
                test: /\.css$/,
                include: path.resolve(__dirname, 'src/standalone'),
                type: 'asset/source',
            },
        ],
    },
    resolve: {
        extensions: ['.js', '.json', '.css'],
    },
    optimization: {
        minimize: argv.mode === 'production',
        minimizer: [
            new TerserPlugin({
                extractComments: false,
                terserOptions: {
                    ecma: 2020,
                    mangle: true,
                    compress: {
                        drop_console: false,
                        passes: 2,
                    },
                    output: {
                        comments: false,
                        beautify: false,
                        wrap_iife: true,
                    },
                },
            }),
        ],
    },
    plugins: [
        new webpack.ProgressPlugin(),
        new webpack.DefinePlugin({
            'process.env.NODE_ENV': JSON.stringify(argv.mode || 'production'),
        }),
    ],
});
