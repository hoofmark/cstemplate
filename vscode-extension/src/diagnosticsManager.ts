import * as vscode from 'vscode';
import { CliDiagnostic } from './cliTypes';

/**
 * Manages the VS Code DiagnosticCollection for cstemplate template files.
 * Translates CliDiagnostic objects (from `cstemplate check --json`) into
 * VS Code Diagnostic entries, which appear as red/yellow squiggles
 * in the editor and entries in the Problems panel.
 */
export class DiagnosticsManager implements vscode.Disposable {

  private readonly collection: vscode.DiagnosticCollection;

  constructor() {
    this.collection = vscode.languages.createDiagnosticCollection('cstemplate');
  }

  /**
   * Publishes diagnostics for a template file.
   * Clears any existing diagnostics for that file first.
   */
  publish(templateUri: vscode.Uri, diagnostics: CliDiagnostic[]): void {
    const vsDiagnostics = diagnostics.map(d => this.toVsDiagnostic(d, templateUri));
    this.collection.set(templateUri, vsDiagnostics);
  }

  /** Clears all diagnostics for a specific file. */
  clear(templateUri: vscode.Uri): void {
    this.collection.delete(templateUri);
  }

  /** Clears all cstemplate diagnostics across all files. */
  clearAll(): void {
    this.collection.clear();
  }

  dispose(): void {
    this.collection.dispose();
  }

  // ---------------------------------------------------------------------------

  private toVsDiagnostic(d: CliDiagnostic, fallbackUri: vscode.Uri): vscode.Diagnostic {
    // Lines and columns from Roslyn are 1-based; VS Code ranges are 0-based
    const line   = Math.max(0, d.line - 1);
    const column = Math.max(0, d.column - 1);

    // Use a single-character range at the reported position.
    // Roslyn provides end positions too, but our CliDiagnostic only carries start
    // for now — a full range can be added later if desired.
    const range = new vscode.Range(line, column, line, column + 1);

    const diagnostic = new vscode.Diagnostic(
      range,
      d.message,
      this.toVsSeverity(d.severity)
    );

    diagnostic.source = 'cstemplate';

    // If Roslyn reports the error in a different file than the template
    // (unlikely but possible for multi-file scenarios), attach a related info link
    if (d.filePath) {
      const errorUri = vscode.Uri.file(d.filePath);
      if (errorUri.toString() !== fallbackUri.toString()) {
        diagnostic.relatedInformation = [
          new vscode.DiagnosticRelatedInformation(
            new vscode.Location(errorUri, range),
            'Error location'
          )
        ];
      }
    }

    return diagnostic;
  }

  private toVsSeverity(severity: CliDiagnostic['severity']): vscode.DiagnosticSeverity {
    switch (severity) {
      case 'error':   return vscode.DiagnosticSeverity.Error;
      case 'warning': return vscode.DiagnosticSeverity.Warning;
      case 'info':    return vscode.DiagnosticSeverity.Information;
      default:        return vscode.DiagnosticSeverity.Error;
    }
  }
}
