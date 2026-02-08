import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'Graft',
  description: 'A CLI tool for managing stacked branches and git worktrees',

  base: '/Graft/',
  appearance: 'dark',
  cleanUrls: true,

  head: [
    ['link', { rel: 'preconnect', href: 'https://fonts.googleapis.com' }],
    ['link', { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' }],
    ['link', { href: 'https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;600;700&display=swap', rel: 'stylesheet' }],
  ],

  themeConfig: {
    search: {
      provider: 'local',
    },

    nav: [
      { text: 'Download', link: '/#install' },
      { text: 'Portfolio', link: 'https://radaiko.github.io' },
    ],

    sidebar: [
      {
        text: 'Guide',
        items: [
          { text: 'Home', link: '/' },
          { text: 'Workflow Guide', link: '/workflow' },
          { text: 'Web UI', link: '/web-ui' },
          { text: 'FAQ', link: '/faq' },
        ],
      },
      {
        text: 'CLI Reference',
        link: '/cli-reference',
        items: [
          { text: 'Stack Commands', link: '/cli/stack' },
          { text: 'Worktree Commands', link: '/cli/worktree' },
          { text: 'Scan & Discovery', link: '/cli/scan' },
          { text: 'Navigation', link: '/cli/navigation' },
          { text: 'Status', link: '/cli/status' },
          { text: 'Nuke Commands', link: '/cli/nuke' },
          { text: 'Conflict Resolution', link: '/cli/conflict' },
          { text: 'Setup Commands', link: '/cli/setup' },
        ],
      },
      {
        text: 'Changelog',
        link: '/changelog',
        items: [
          { text: 'CLI', link: '/changelog/cli' },
          { text: 'VS Code Extension', link: '/changelog/vscode' },
          { text: 'Visual Studio', link: '/changelog/vs' },
          { text: 'JetBrains Plugin', link: '/changelog/jetbrains' },
        ],
      },
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/radaiko/Graft' },
    ],
  },
})
