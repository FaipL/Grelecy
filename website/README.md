<!-- SPDX-License-Identifier: CC-BY-4.0 -->

# GRELECY project website

A static, bilingual presentation of GRELECY. French is at the site root and English at `en/`. Both are complete HTML pages: navigation and language selection work without JavaScript. There are no third-party runtime requests, cookies, analytics, package dependencies, or backend services. Inter v4.1 is served locally with its SIL Open Font License in `fonts/LICENSE.txt`.

## Community scope

The site presents **Kepler, Maxwell, Pascal, and Turing-based GeForce GTX 16 series cards** as the initial community scope on Windows. This Turing addition is specifically the GTX 16 series. Keep the shared scope list, both translations, and the [project brief](../docs/PROJECT_SCOPE.md) consistent. The list does not define a technical implementation order or guarantee support.

## Edit and preview

From the repository root, with Python 3.12:

```sh
python3 scripts/build_site.py
python3 scripts/check_site.py
python3 -m http.server 8000 --directory _site --bind 127.0.0.1
```

The two pages are served at `/` and `/en/`. `_site/` is generated and ignored by Git. Stop the local server with Ctrl+C.

- Edit `locales/fr.json` and `locales/en.json` together. Keep their keys aligned.
- Shared markup is in `index.template.html`; styling is in `site.css`.
- The build copies the existing official logo and banner from `assets/brand/` without changing them.
- All contributions go through the repository's existing GitHub issue forms.

## GitHub Pages

In the repository's **Settings → Pages → Build and deployment**, select **GitHub Actions** as the source. If necessary, run **Actions → Project website → Run workflow** after enabling Pages. The workflow builds and checks the website, then deploys pushes to `main`. Pull requests build and validate without deploying.

The intended public URLs are `https://faipl.github.io/Grelecy/` and `https://faipl.github.io/Grelecy/en/`. These URLs are deployment targets, not confirmation that Pages has been enabled or a deployment has succeeded.

For a future custom domain, pass its HTTPS base URL to `scripts/build_site.py --base-url https://example.org` in the workflow and configure the domain through GitHub Pages. The flag controls canonical URLs, language metadata, the social image, and sitemap; navigation and assets use relative paths.

## Licensing

Website template, stylesheet, build/check scripts and deployment workflow: MPL-2.0. Original prose and translations: CC BY 4.0, adapted from the GRELECY contributors' project documentation. Logo and banner artwork retain their separate brand terms. See [LICENSING.md](../LICENSING.md) and [BRANDING.md](../BRANDING.md).

