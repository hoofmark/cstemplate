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

export interface DebugReadyInfo {
  pid: number;
  /** Resolves when the template process exits, with its final CliOutput. */
  completion: Promise<CliOutput>;
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

  /**
   * Starts a debug run. Returns as soon as the CLI has printed its PID and is
   * waiting for a debugger — the caller is responsible for attaching and then
   * awaiting the returned completion promise for the final result.
   *
   * Rejects if the CLI fails to start or does not emit a waitingForDebugger
   * message within a reasonable timeout.
   */
  async runDebug(options: RunOptions): Promise<DebugReadyInfo> {
    const args = ['run', options.templatePath, '--json', '--debug'];

    if (options.outputRoot) {
      args.push('--output', options.outputRoot);
    } else {
      const settingRoot = this.outputRoot;
      if (settingRoot && !(await this.hasWorkspaceConfig(options.templatePath))) {
        args.push('--output', settingRoot);
      }
    }
    if (options.configPath) { args.push('--config', options.configPath); }

    const cwd = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

    return new Promise((resolveReady, rejectReady) => {
      let stdoutBuffer = '';
      let stderrBuffer = '';
      let debugReadyResolved = false;

      // Will be resolved/rejected when the process exits
      let resolveCompletion!: (result: CliOutput) => void;
      let rejectCompletion!:  (err: Error) => void;
      const completion = new Promise<CliOutput>((res, rej) => {
        resolveCompletion = res;
        rejectCompletion  = rej;
      });

      const proc = cp.spawn(this.cliPath, args, { cwd, shell: false });

      // Timeout: if we don't hear waitingForDebugger within 45s, give up
      const timeout = setTimeout(() => {
        if (!debugReadyResolved) {
          proc.kill();
          rejectReady(new Error(
            'cstemplate did not emit a waitingForDebugger message within 45 seconds.'
          ));
        }
      }, 45_000);

      proc.stdout.on('data', (chunk: Buffer) => {
        stdoutBuffer += chunk.toString();

        // The CLI emits one JSON object per line — scan for the debug-ready line
        if (!debugReadyResolved) {
          const lines = stdoutBuffer.split('\n');
          for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed) { continue; }
            try {
              const parsed = JSON.parse(trimmed);
              if (parsed.status === 'waitingForDebugger' && typeof parsed.pid === 'number') {
                debugReadyResolved = true;
                clearTimeout(timeout);
                resolveReady({ pid: parsed.pid, completion });
                return;
              }
            } catch {
              // Not JSON yet — keep buffering
            }
          }
        }
      });

      proc.stderr.on('data', (chunk: Buffer) => {
        stderrBuffer += chunk.toString();
      });

      proc.on('error', (err) => {
        clearTimeout(timeout);
        if (!debugReadyResolved) {
          rejectReady(err);
        } else {
          rejectCompletion(err);
        }
      });

      proc.on('close', () => {
        clearTimeout(timeout);
        const trimmed = stdoutBuffer.trim();

        // Strip the waitingForDebugger line — the final output is the last JSON object
        const lines = trimmed.split('\n').map((l: string) => l.trim()).filter(Boolean);
        const lastLine = lines[lines.length - 1] ?? '';

        try {
          const result = JSON.parse(lastLine) as CliOutput;
          if (result.status !== 'waitingForDebugger') {
            resolveCompletion(result);
            return;
          }
        } catch { /* fall through */ }

        // No usable final output
        resolveCompletion({
          status: 'error',
          message: stderrBuffer.trim() || 'cstemplate produced no output after debugging.',
        });
      });
    });
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
