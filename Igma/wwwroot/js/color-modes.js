(() => {
  const STORAGE_KEY = 'theme'

  const getStoredTheme = () => localStorage.getItem(STORAGE_KEY)
  const setStoredTheme = theme => localStorage.setItem(STORAGE_KEY, theme)

  // Default to dark when nothing is stored
  const getPreferredTheme = () => getStoredTheme() || 'dark'

  const setTheme = theme => {
    if (theme === 'auto' && window.matchMedia('(prefers-color-scheme: dark)').matches) {
      document.documentElement.setAttribute('data-coreui-theme', 'dark')
    } else {
      document.documentElement.setAttribute('data-coreui-theme', theme)
    }
  }

  // Apply before CSS loads to prevent flash
  setTheme(getPreferredTheme())

  const showActiveTheme = theme => {
    const btnToActive = document.querySelector(`[data-coreui-theme-value="${theme}"]`)
    if (!btnToActive) return

    for (const el of document.querySelectorAll('[data-coreui-theme-value]')) {
      el.classList.remove('active')
    }
    btnToActive.classList.add('active')

    // Update the toggle icon to match the active theme
    const activeIcon = document.querySelector('.theme-icon-active use')
    const srcIcon = btnToActive.querySelector('svg use')
    if (activeIcon && srcIcon) {
      activeIcon.setAttribute('href', srcIcon.getAttribute('href'))
    }
  }

  window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
    if (!getStoredTheme()) setTheme(getPreferredTheme())
  })

  window.addEventListener('DOMContentLoaded', () => {
    showActiveTheme(getPreferredTheme())

    for (const btn of document.querySelectorAll('[data-coreui-theme-value]')) {
      btn.addEventListener('click', () => {
        const theme = btn.getAttribute('data-coreui-theme-value')
        setStoredTheme(theme)
        setTheme(theme)
        showActiveTheme(theme)
      })
    }
  })
})()
