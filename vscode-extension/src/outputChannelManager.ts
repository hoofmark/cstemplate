import * as vscode from 'vscode';
import { CliOutput } from './cliTypes';

/**
 * Manages the "cstemplate" output channel — the panel shown in the
 * VS Code Output tab when templates are run.
 */
export class OutputChannelManager implements vscode.Disposable {

  private readonly channel: vscode.OutputChannel;

  constructor() {
    this.channel = vscode.window.createOutputChannel('cstemplate');
  }

  /** Writes a successful run summary to the output channel. */
  reportSuccess(output: CliOutput): void {
    this.channel.appendLine('');
    this.channel.appendLine(`✓ Template '${output.templateName}' completed successfully.`);
    this.channel.appendLine(`  Output root: ${output.outputRoot}`);
    this.channel.appendLine(`  Generated ${output.fileCount} file(s):`);
    for (const file of output.generatedFiles ?? []) {
      this.channel.appendLine(`    → ${file}`);
    }
    this.channel.appendLine('');
  }

  /** Writes compilation failure details to the output channel. */
  reportCompilationFailure(output: CliOutput, templatePath: string): void {
    this.channel.appendLine('');
    this.channel.appendLine(`✗ Template '${templatePath}' failed to compile.`);
    for (const d of output.diagnostics ?? []) {
      const loc = d.filePath ? `${d.filePath}(${d.line},${d.column})` : `(${d.line},${d.column})`;
      this.channel.appendLine(`  [${d.severity.toUpperCase()}] ${loc}: ${d.message}`);
    }
    this.channel.appendLine('');
  }

  /** Writes an execution failure to the output channel. */
  reportExecutionFailure(output: CliOutput, templatePath: string): void {
    this.channel.appendLine('');
    this.channel.appendLine(`✗ Template '${output.templateName ?? templatePath}' threw an exception during execution.`);
    if (output.innerExceptionType) {
      this.channel.appendLine(`  ${output.innerExceptionType}: ${output.innerMessage ?? ''}`);
    } else if (output.innerMessage) {
      this.channel.appendLine(`  ${output.innerMessage}`);
    }
    if (output.stackTrace) {
      this.channel.appendLine('');
      this.channel.appendLine('  Stack trace:');
      for (const line of output.stackTrace.split('\n')) {
        this.channel.appendLine(`  ${line.trimStart()}`);
      }
    }
    this.channel.appendLine('');
  }

  /** Writes a generic error (e.g. CLI not found) to the output channel. */
  reportError(message: string): void {
    this.channel.appendLine('');
    this.channel.appendLine(`✗ ${message}`);
    this.channel.appendLine('');
  }

  /** Writes a header line marking the start of a new run. */
  beginRun(templatePath: string): void {
    const timestamp = new Date().toLocaleTimeString();
    this.channel.appendLine(`[${timestamp}] Running template: ${templatePath}`);
  }

  /** Shows the output panel. */
  show(): void {
    this.channel.show(true /* preserveFocus */);
  }

  dispose(): void {
    this.channel.dispose();
  }
}
