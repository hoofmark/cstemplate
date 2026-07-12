import * as vscode from 'vscode';
import { CliRunner } from './cliRunner';
import { DiagnosticsManager } from './diagnosticsManager';
import { OutputChannelManager } from './outputChannelManager';
import { TemplateCommands } from './templateCommands';

/**
 * Called once when the extension is activated.
 * Wires up all services, registers commands, and sets up event listeners.
 */
export function activate(context: vscode.ExtensionContext): void {

  // ── Services ──────────────────────────────────────────────────────────────

  const cli         = new CliRunner();
  const diagnostics = new DiagnosticsManager();
  const output      = new OutputChannelManager();
  const commands    = new TemplateCommands(cli, diagnostics, output);

  context.subscriptions.push(diagnostics, output);

  // ── Commands ──────────────────────────────────────────────────────────────

  context.subscriptions.push(
    vscode.commands.registerCommand(
      'cstemplate.runTemplate',
      (uri?: vscode.Uri) => commands.runTemplate(uri)
    ),

    vscode.commands.registerCommand(
      'cstemplate.runTemplateDebug',
      (uri?: vscode.Uri) => commands.runTemplateDebug(uri)
    ),

    vscode.commands.registerCommand(
      'cstemplate.checkTemplate',
      (uri?: vscode.Uri) => commands.checkTemplate(uri)
    ),

    vscode.commands.registerCommand(
      'cstemplate.runAllTemplates',
      () => commands.runAllTemplates()
    ),

    vscode.commands.registerCommand(
      'cstemplate.revealOutput',
      (uri?: vscode.Uri) => commands.revealOutput(uri)
    ),
  );

  // ── Event Listeners ───────────────────────────────────────────────────────

  // Run `cstemplate check` on save for template files
  context.subscriptions.push(
    vscode.workspace.onDidSaveTextDocument(async (document) => {
      if (isTemplateDocument(document)) {
        await commands.onDidSaveTemplate(document);
      }
    })
  );

  // Clear diagnostics when a template file is closed
  context.subscriptions.push(
    vscode.workspace.onDidCloseTextDocument((document) => {
      if (isTemplateDocument(document)) {
        diagnostics.clear(document.uri);
      }
    })
  );

  // Set a context key so package.json `when` clauses can use cstemplate.isTemplateFile
  context.subscriptions.push(
    vscode.window.onDidChangeActiveTextEditor((editor) => {
      const isTemplate = editor ? isTemplateDocument(editor.document) : false;
      vscode.commands.executeCommand(
        'setContext',
        'cstemplate.isTemplateFile',
        isTemplate
      );
    })
  );

  // Set initial context for whatever is already open on activation
  const activeEditor = vscode.window.activeTextEditor;
  if (activeEditor) {
    vscode.commands.executeCommand(
      'setContext',
      'cstemplate.isTemplateFile',
      isTemplateDocument(activeEditor.document)
    );
  }
}

export function deactivate(): void {
  // Disposables registered via context.subscriptions are cleaned up automatically.
  // Nothing additional needed here.
}

// ---------------------------------------------------------------------------

function isTemplateDocument(document: vscode.TextDocument): boolean {
  return document.uri.fsPath.endsWith('.template.cs');
}
