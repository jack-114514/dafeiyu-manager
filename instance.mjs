import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

export const managerDir = path.dirname(fileURLToPath(import.meta.url))
export const privateDir = path.join(managerDir, 'private')

export function readInstance() {
  const file = path.join(privateDir, 'instance.json')
  const config = JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''))
  for (const name of ['runtimeDir', 'harnessExe', 'harnessProfileDir', 'fishHome', 'workspaceDir']) {
    if (typeof config[name] !== 'string' || !path.isAbsolute(config[name])) {
      throw new Error(`${name} must be an absolute path in private/instance.json`)
    }
    config[name] = config[name].replace(/\\/g, '/').replace(/\/$/, '')
  }
  for (const name of ['botQQ', 'ownerQQ']) {
    if (typeof config[name] !== 'string' || !/^\d{5,12}$/.test(config[name])) {
      throw new Error(`${name} must be a QQ number string in private/instance.json`)
    }
  }
  return config
}
