import * as cp from 'child_process';
import * as vscode from 'vscode';
import { CliOutput } from './cliTypes';

export interface RunOptions {
  templatePath: string;
  outputRoot?: string;
  configPath?: string;
}

export interface CheckOptions {
  templatePath: string;
}

/**
 * Spawns the cstemplate CLI and returns the parsed JSON output.
 * All CLI invocations go through here — commands and the on-save checker
 * both use this module.
 */
export class CliRunner {

  private get cliPath(): string {
    return vscode.workspace
      .getConfiguration('cstemplate')
      .get<string>('cliPath', 'cstemplate');
  }

  private get outputRoot(): string {
    return vscode.workspace
      .getConfiguration('cstemplate')
      .get<string>('outputRoot', '');
  }

  /**
   * Returns true if a cstemplate.config.json exists at or above the given
   * file path. When one is present the CLI's own outputRoot takes precedence
   * and we should not pass --output from the VS Code setting.
   */
  private async hasWorkspaceConfig(templatePath: string): Promise<boolean> {
    const { promises: fs } = await import('fs');
    const path = await import('path');
    let dir = path.dirname(templatePath);
    const root = path.parse(dir).root;
    while (true) {
      try {
        await fs.access(path.join(dir, 'cstemplate.config.json'));
        return true;
      } catch { /* not found at this level */ }
      if (dir === root) { return false; }
      dir = path.dirname(dir);
    }
  }

  async run(options: RunOptions): Promise<CliOutput> {
    const args = ['run', options.templatePath, '--json'];

    // Only pass --output from the VS Code setting when:
    //   (a) an explicit outputRoot was provided to this call, OR
    //   (b) the VS Code setting is non-empty AND no cstemplate.config.json
    //       exists (config file outputRoot takes precedence over the setting)
    const explicitRoot = options.outputRoot;
    if (explicitRoot) {
      args.push('--output', explicitRoot);
    } else {
      const settingRoot = this.outputRoot;
      if (settingRoot && !(await this.hasWorkspaceConfig(options.templatePath))) {
        args.push('--output', settingRoot);
      }
    }
    if (options.configPath) {
      args.push('--config', options.configPath);
    }

    return this.invoke(args, options.templatePath);
  }

  async check(options: CheckOptions): Promise<CliOutput> {
    const args = ['check', options.templatePath, '--json'];
    return this.invoke(args, options.templatePath);
  }

  private invoke(args: string[], templatePath: string): Promise<CliOutput> {
    return new Promise((resolve) => {
      let stdout = '';
      let stderr = '';

      const cwd = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

      const proc = cp.spawn(this.cliPath, args, {
        cwd,
        shell: false,
      });

      proc.stdout.on('data', (chunk: Buffer) => { stdout += chunk.toString(); });
      proc.stderr.on('data', (chunk: Buffer) => { stderr += chunk.toString(); });

      proc.on('error', (err) => {
        // CLI not found on PATH or not executable
        if ((err as NodeJS.ErrnoException).code === 'ENOENT') {
          resolve({
            status: 'error',
            message:
              `cstemplate CLI not found at '${this.cliPath}'. ` +
              `Install it with 'dotnet tool install --global HoofMark.CSharpTemplating.Cli', ` +
              `or set 'cstemplate.cliPath' in settings to point to your local build.`,
          });
        } else {
          resolve({ status: 'error', message: err.message });
        }
      });

      proc.on('close', () => {
        const trimmed = stdout.trim();

        if (!trimmed) {
          // No JSON output — fall back to raw stderr
          resolve({
            status: 'error',
            message: stderr.trim() || 'cstemplate produced no output.',
          });
          return;
        }

        try {
          resolve(JSON.parse(trimmed) as CliOutput);
        } catch {
          resolve({
            status: 'error',
            message: `Failed to parse cstemplate output: ${trimmed}`,
          });
        }
      });
    });
  }
}
