#!/usr/bin/env node

/**
 * OpenTrace Homepage Build Script
 * 
 * This script generates static HTML pages for each language defined in i18n.json.
 * It uses the template.html file as a base and replaces placeholders with translations.
 * 
 * Output structure:
 *   dist/
 *     index.html        (English version - default)
 *     zh/
 *       index.html      (Chinese version)
 *     img/              (copied from source)
 *     favicon.ico       (copied from source)
 * 
 * Usage:
 *   node build.js
 * 
 * For Cloudflare Pages:
 *   Add "npm run build" to your build command in Cloudflare Pages settings
 */

const fs = require('fs');
const path = require('path');

// Configuration
const SRC_DIR = __dirname;
const DIST_DIR = path.join(__dirname, 'dist');
const TEMPLATE_FILE = path.join(SRC_DIR, 'template.html');
const I18N_FILE = path.join(SRC_DIR, 'i18n.json');

// Default language (root path)
const DEFAULT_LANG = 'en';

// Supported languages with their output paths
const LANG_PATHS = {
    'en': '',      // Root path for English
    'zh': 'zh'     // /zh/ path for Chinese
};

/**
 * Ensure directory exists
 */
function ensureDir(dir) {
    if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
    }
}

/**
 * Copy directory recursively
 */
function copyDir(src, dest) {
    ensureDir(dest);
    const entries = fs.readdirSync(src, { withFileTypes: true });

    for (const entry of entries) {
        const srcPath = path.join(src, entry.name);
        const destPath = path.join(dest, entry.name);

        if (entry.isDirectory()) {
            copyDir(srcPath, destPath);
        } else {
            fs.copyFileSync(srcPath, destPath);
        }
    }
}

/**
 * Generate hreflang link tags for all languages
 */
function generateHreflangTags(baseUrl) {
    const tags = [];

    for (const [lang, langPath] of Object.entries(LANG_PATHS)) {
        const i18n = JSON.parse(fs.readFileSync(I18N_FILE, 'utf8'));
        const locale = i18n[lang].locale;
        const url = langPath ? `${baseUrl}/${langPath}/` : `${baseUrl}/`;
        tags.push(`<link rel="alternate" hreflang="${locale}" href="${url}">`);
    }

    // Add x-default pointing to default language
    tags.push(`<link rel="alternate" hreflang="x-default" href="${baseUrl}/">`);

    return tags.join('\n    ');
}

/**
 * Generate canonical URL for the current language
 */
function generateCanonicalUrl(baseUrl, langPath) {
    return langPath ? `${baseUrl}/${langPath}/` : `${baseUrl}/`;
}

/**
 * Replace template placeholders with translations
 */
function processTemplate(template, lang, translations, baseUrl) {
    let output = template;

    // Get current language translations
    const t = translations[lang];

    // Replace all {{key}} placeholders with corresponding translations
    for (const [key, value] of Object.entries(t)) {
        const regex = new RegExp(`{{${key}}}`, 'g');
        output = output.replace(regex, value);
    }

    // Generate and insert hreflang tags
    const hreflangTags = generateHreflangTags(baseUrl);
    output = output.replace('{{hreflang_tags}}', hreflangTags);

    // Generate and insert canonical URL
    const langPath = LANG_PATHS[lang];
    const canonicalUrl = generateCanonicalUrl(baseUrl, langPath);
    output = output.replace(/{{canonical_url}}/g, canonicalUrl);

    // Set base URL for relative paths
    // For non-root languages, we need to adjust relative paths
    if (langPath) {
        // Adjust relative paths for nested language directories
        output = output.replace(/src="\.\/img\//g, 'src="../img/');
        output = output.replace(/href="\.\/img\//g, 'href="../img/');
        output = output.replace(/src="\.\/favicon/g, 'src="../favicon');
        output = output.replace(/href="\.\/favicon/g, 'href="../favicon');
    } else {
        // For root, just remove the leading ./
        output = output.replace(/src="\.\/img\//g, 'src="./img/');
        output = output.replace(/href="\.\/img\//g, 'href="./img/');
    }

    return output;
}

/**
 * Main build function
 */
function build() {
    console.log('🚀 Starting OpenTrace Homepage build...\n');

    // Read configuration
    const baseUrl = process.env.SITE_URL || 'https://opentrace.app';
    console.log(`📍 Base URL: ${baseUrl}`);

    // Load template and translations
    if (!fs.existsSync(TEMPLATE_FILE)) {
        console.error('❌ Error: template.html not found!');
        process.exit(1);
    }

    if (!fs.existsSync(I18N_FILE)) {
        console.error('❌ Error: i18n.json not found!');
        process.exit(1);
    }

    const template = fs.readFileSync(TEMPLATE_FILE, 'utf8');
    const translations = JSON.parse(fs.readFileSync(I18N_FILE, 'utf8'));

    console.log(`📝 Loaded ${Object.keys(translations).length} language(s): ${Object.keys(translations).join(', ')}\n`);

    // Clean and create dist directory
    if (fs.existsSync(DIST_DIR)) {
        fs.rmSync(DIST_DIR, { recursive: true });
    }
    ensureDir(DIST_DIR);

    // Copy static assets
    console.log('📁 Copying static assets...');

    const imgSrcDir = path.join(SRC_DIR, 'img');
    const imgDestDir = path.join(DIST_DIR, 'img');
    if (fs.existsSync(imgSrcDir)) {
        copyDir(imgSrcDir, imgDestDir);
        console.log('   ✓ Copied img/ directory');
    }

    const faviconSrc = path.join(SRC_DIR, 'favicon.ico');
    const faviconDest = path.join(DIST_DIR, 'favicon.ico');
    if (fs.existsSync(faviconSrc)) {
        fs.copyFileSync(faviconSrc, faviconDest);
        console.log('   ✓ Copied favicon.ico');
    }

    console.log('');

    // Generate HTML for each language
    console.log('🌐 Generating localized pages...');

    for (const [lang, langPath] of Object.entries(LANG_PATHS)) {
        if (!translations[lang]) {
            console.warn(`   ⚠ Warning: No translations found for "${lang}", skipping...`);
            continue;
        }

        // Determine output path
        const outputDir = langPath ? path.join(DIST_DIR, langPath) : DIST_DIR;
        ensureDir(outputDir);

        // Process template
        const html = processTemplate(template, lang, translations, baseUrl);

        // Write output file
        const outputFile = path.join(outputDir, 'index.html');
        fs.writeFileSync(outputFile, html, 'utf8');

        const relativePath = langPath ? `/${langPath}/index.html` : '/index.html';
        console.log(`   ✓ Generated ${relativePath} (${lang})`);
    }

    console.log('\n✅ Build completed successfully!');
    console.log(`📂 Output directory: ${DIST_DIR}`);
}

// Run build
build();
