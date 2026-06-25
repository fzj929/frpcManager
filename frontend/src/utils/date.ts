const timezonePattern = /(Z|[+-]\d{2}:?\d{2})$/i

export function formatDateTime(value: string | null | undefined, fallback = '-') {
  if (!value) return fallback

  const normalized = normalizeBackendDateTime(value)
  const date = new Date(normalized)
  if (Number.isNaN(date.getTime())) return fallback

  return date.toLocaleString()
}

function normalizeBackendDateTime(value: string) {
  const trimmed = value.trim()
  if (!trimmed) return trimmed
  if (timezonePattern.test(trimmed)) return trimmed

  return trimmed.includes('T')
    ? `${trimmed}Z`
    : `${trimmed.replace(' ', 'T')}Z`
}
