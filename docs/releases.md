---
layout: default
title: Releases
nav_order: 6
---

# Release Notes

All Graft CLI releases. Download binaries from the [GitHub Releases](https://github.com/radaiko/Graft/releases) page.

---

{% assign releases = site.pages | where_exp: "page", "page.path contains 'release/cli/'" | sort: "version" | reverse %}
{% for release in releases %}
## [{{ release.title }}]({{ release.url | relative_url }})

{{ release.description }}

---

{% endfor %}
