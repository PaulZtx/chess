interface WebMcpTool {
  name: string
  title: string
  description: string
  inputSchema: object
  annotations: { readOnlyHint: boolean; untrustedContentHint: boolean }
  execute: (input: unknown) => unknown | Promise<unknown>
}

interface WebMcpDocument extends Document {
  modelContext?: {
    registerTool: (tool: WebMcpTool, options?: { signal?: AbortSignal }) => void | Promise<void>
  }
}

export function registerWebMcpTools(tools: WebMcpTool[]) {
  const context = (document as WebMcpDocument).modelContext
  if (!context?.registerTool) return undefined

  const lifecycle = new AbortController()
  for (const tool of tools) {
    try {
      void Promise.resolve(context.registerTool(tool, { signal: lifecycle.signal })).catch(() => undefined)
    } catch {
      // The visible UI remains fully functional when WebMCP is unavailable.
    }
  }

  return () => lifecycle.abort()
}
