import * as vscode from 'vscode';
import * as path from 'path';
import { CliRunner } from './cliRunner';
import { DiagnosticsManager } from './diagnosticsManager';
import { OutputChannelManager } from './outputChannelManager';

/**
 * Implements all cstemplate commands. Each public method maps to a command
 * registered in extension.ts.
 */
export class TemplateCommands {

  constructor(
    private readonly cli: CliRunner,
    private readonly diagnostics: DiagnosticsManager,
    private readonly output: OutputChannelManager,
  ) {}

  // ---------------------------------------------------------------------------
  // cstemplate.runTemplate
  // ---------------------------------------------------------------------------

  async runTemplate(uri?: vscode.Uri): Promise<void> {
    const templateUri = await this.resolveTemplateUri(uri);
    if (!templateUri) { return; }

    const templatePath = templateUri.fsPath;

    this.output.beginRun(templatePath);

    const showOutput = vscode.workspace
      .getConfiguration('cstemplate')
      .get<boolean>('showOutputOnRun', true);

    if (showOutput) {
      this.output.show();
    }

    await vscode.window.withProgress(
      {
        location: vscode.ProgressLocation.Notification,
        title: `cstemplate: Running ${path.basename(templatePath)}…`,
        cancellable: false,
      },
      async () => {
        const result = await this.cli.run({ templatePath });

        switch (result.status) {
          case 'success':
            this.diagnostics.clear(templateUri);
            this.output.reportSuccess(result);
            vscode.window.showInformationMessage(
              `cstemplate: Generated ${result.fileCount} file(s) from '${result.templateName}'.`,
              'Reveal Output'
            ).then(choice => {
              if (choice === 'Reveal Output' && result.outputRoot) {
                vscode.commands.executeCommand(
                  'revealFileInOS',
                  vscode.Uri.file(result.outputRoot)
                );
              }
            });
            break;

          case 'compilationError':
            this.output.reportCompilationFailure(result, templatePath);
            this.diagnostics.publish(templateUri, result.diagnostics ?? []);
            vscode.window.showErrorMessage(
              `cstemplate: '${path.basename(templatePath)}' failed to compile. ` +
              `See Problems panel for details.`
            );
            break;

          case 'executionError':
            this.diagnostics.clear(templateUri);
            this.output.reportExecutionFailure(result, templatePath);
            vscode.window.showErrorMessage(
              `cstemplate: '${path.basename(templatePath)}' threw an exception. ` +
              `See Output panel for details.`
            );
            break;

          default:
            this.output.reportError(result.message ?? 'Unknown error.');
            vscode.window.showErrorMessage(`cstemplate error: ${result.message}`);
            break;
        }
      }
    );
  }

  // ---------------------------------------------------------------------------
  // cstemplate.checkTemplate
  // ---------------------------------------------------------------------------

  async checkTemplate(uri?: vscode.Uri): Promise<void> {
    const templateUri = await this.resolveTemplateUri(uri);
    if (!templateUri) { return; }

    const templatePath = templateUri.fsPath;
    const result = await this.cli.check({ templatePath });

    if (result.status === 'success') {
      this.diagnostics.clear(templateUri);
      vscode.window.setStatusBarMessage(
        `$(check) cstemplate: '${result.templateName}' compiled OK`,
        4000
      );
    } else if (result.status === 'compilationError') {
      this.diagnostics.publish(templateUri, result.diagnostics ?? []);
      vscode.window.setStatusBarMessage(
        `$(error) cstemplate: ${result.diagnostics?.length ?? 0} compile error(s)`,
        4000
      );
    } else {
      this.output.reportError(result.message ?? 'Unknown error.');
      vscode.window.showErrorMessage(`cstemplate check error: ${result.message}`);
    }
  }

  // ---------------------------------------------------------------------------
  // cstemplate.runAllTemplates
  // ---------------------------------------------------------------------------

  async runAllTemplates(): Promise<void> {
    const pattern = vscode.workspace
      .getConfiguration('cstemplate')
      .get<string>('templateFilePattern', '**/*.template.cs');

    const files = await vscode.workspace.findFiles(pattern, '**/node_modules/**');

    if (files.length === 0) {
      vscode.window.showInformationMessage(
        `cstemplate: No template files found matching '${pattern}'.`
      );
      return;
    }

    const confirm = await vscode.window.showInformationMessage(
      `cstemplate: Run all ${files.length} template(s) in workspace?`,
      { modal: true },
      'Run All'
    );
    if (confirm !== 'Run All') { return; }

    this.output.show();

    let successCount = 0;
    let errorCount   = 0;

    await vscode.window.withProgress(
      {
        location: vscode.ProgressLocation.Notification,
        title: 'cstemplate: Running all templates…',
        cancellable: false,
      },
      async (progress) => {
        for (let i = 0; i < files.length; i++) {
          const file = files[i];
          progress.report({
            message: `${i + 1}/${files.length}: ${path.basename(file.fsPath)}`,
            increment: (1 / files.length) * 100,
          });

          const result = await this.cli.run({ templatePath: file.fsPath });

          if (result.status === 'success') {
            this.diagnostics.clear(file);
            this.output.reportSuccess(result);
            successCount++;
          } else {
            errorCount++;
            if (result.status === 'compilationError') {
              this.diagnostics.publish(file, result.diagnostics ?? []);
              this.output.reportCompilationFailure(result, file.fsPath);
            } else {
              this.output.reportExecutionFailure(result, file.fsPath);
            }
          }
        }
      }
    );

    const summary = `cstemplate: ${successCount} succeeded, ${errorCount} failed.`;
    if (errorCount > 0) {
      vscode.window.showWarningMessage(summary);
    } else {
      vscode.window.showInformationMessage(summary);
    }
  }

  // ---------------------------------------------------------------------------
  // cstemplate.revealOutput
  // ---------------------------------------------------------------------------

  async revealOutput(uri?: vscode.Uri): Promise<void> {
    const templateUri = await this.resolveTemplateUri(uri);
    if (!templateUri) { return; }

    const templateDir  = path.dirname(templateUri.fsPath);
    const outputRoot   = vscode.workspace.getConfiguration('cstemplate').get<string>('outputRoot', '');
    const resolvedRoot = outputRoot || path.join(templateDir, 'generated');

    vscode.commands.executeCommand(
      'revealFileInOS',
      vscode.Uri.file(resolvedRoot)
    );
  }

  // ---------------------------------------------------------------------------
  // On-save check (not a command — called from the document save listener)
  // ---------------------------------------------------------------------------

  async onDidSaveTemplate(document: vscode.TextDocument): Promise<void> {
    const checkOnSave = vscode.workspace
      .getConfiguration('cstemplate')
      .get<boolean>('checkOnSave', true);

    if (!checkOnSave) { return; }

    await this.checkTemplate(document.uri);
  }


  // ---------------------------------------------------------------------------
  // cstemplate.runTemplateDebug
  // ---------------------------------------------------------------------------

  async runTemplateDebug(uri?: vscode.Uri): Promise<void> {
    const templateUri = await this.resolveTemplateUri(uri);
    if (!templateUri) { return; }

    const templatePath = templateUri.fsPath;

    this.output.beginRun(templatePath);
    this.output.show();

    vscode.window.showInformationMessage(
      `cstemplate: Starting debug session for '${require('path').basename(templatePath)}'…`
    );

    let debugReady: import('./cliRunner').DebugReadyInfo;
    try {
      debugReady = await this.cli.runDebug({ templatePath });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      vscode.window.showErrorMessage(`cstemplate: Failed to start debug run — ${message}`);
      return;
    }

    // Attach the VS Code .NET debugger to the waiting cstemplate process.
    //
    // justMyCode: false  — the template assembly is dynamically compiled and
    //   has no project context; with justMyCode on the debugger treats it as
    //   external code and skips breakpoints inside it entirely.
    //
    // requireExactSource: false — relaxes the source-path matching check so
    //   breakpoints bind even if the path embedded in the PDB differs slightly
    //   from the URI VS Code uses for the open editor document.
    //
    // symbolOptions.searchMicrosoftSymbolServer / searchNuGetOrgSymbolServer:
    //   false — we don't need symbol servers for our own template assembly.
    //
    // sourceFileMap — maps the embedded source path in the PDB back to the
    //   actual template file so VS Code highlights the correct line.
    const templateDir = require('path').dirname(templatePath);
    const attached = await vscode.debug.startDebugging(
      vscode.workspace.workspaceFolders?.[0],
      {
        type: 'coreclr',
        request: 'attach',
        name: `tgen: ${require('path').basename(templatePath)}`,
        processId: debugReady.pid,
        justMyCode: false,
        requireExactSource: false,
        symbolOptions: {
          searchMicrosoftSymbolServer: false,
          searchNuGetOrgSymbolServer:  false,
          moduleFilter: {
            mode: 'loadAllButExcluded',
            excludedModules: [],
          },
        },
        sourceFileMap: {
          [templatePath]: templatePath,
        },
      }
    );

    if (!attached) {
      vscode.window.showErrorMessage(
        'tgen: Failed to attach debugger. ' +
        'Ensure the C# Dev Kit extension is installed and try again.'
      );
      return;
    }

    // Wait for the debugger to hit the initial Debugger.Break() pause point
    // before signalling the process to continue. This gives the debugger time
    // to load symbols for the template assembly and bind any breakpoints the
    // user has set in the .template.cs file before execution reaches them.
    await new Promise<void>(resolve => {
      const listener = vscode.debug.onDidReceiveDebugSessionCustomEvent(e => {
        if (e.event === 'stopped') {
          listener.dispose();
          resolve();
        }
      });
      // Safety timeout — if the stopped event never arrives, continue anyway
      setTimeout(() => { listener.dispose(); resolve(); }, 10_000);
    });

    // Wait for the template to finish executing
    const result = await debugReady.completion;

    switch (result.status) {
      case 'success':
        this.diagnostics.clear(templateUri);
        this.output.reportSuccess(result);
        vscode.window.showInformationMessage(
          `tgen: Generated ${result.fileCount} file(s) from '${result.templateName}'.`
        );
        break;

      case 'compilationError':
        this.output.reportCompilationFailure(result, templatePath);
        this.diagnostics.publish(templateUri, result.diagnostics ?? []);
        vscode.window.showErrorMessage(
          `tgen: '${require('path').basename(templatePath)}' failed to compile.`
        );
        break;

      case 'executionError':
        this.diagnostics.clear(templateUri);
        this.output.reportExecutionFailure(result, templatePath);
        vscode.window.showErrorMessage(
          `tgen: '${require('path').basename(templatePath)}' threw an exception. ` +
          `See Output panel for details.`
        );
        break;

      default:
        this.output.reportError(result.message ?? 'Unknown error.');
        vscode.window.showErrorMessage(`cstemplate error: ${result.message}`);
        break;
    }
  }

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  /**
   * Resolves the target template URI from:
   *   1. An explicit URI (from context menu / explorer click)
   *   2. The currently active editor
   *   3. A quick-pick if multiple template files are open
   */
  private async resolveTemplateUri(uri?: vscode.Uri): Promise<vscode.Uri | undefined> {
    if (uri) { return uri; }

    const active = vscode.window.activeTextEditor?.document;
    if (active && this.isTemplateFile(active.uri)) {
      return active.uri;
    }

    // No active template — offer a quick-pick from open editors
    const pattern = vscode.workspace
      .getConfiguration('cstemplate')
      .get<string>('templateFilePattern', '**/*.template.cs');

    const files = await vscode.workspace.findFiles(pattern, '**/node_modules/**');

    if (files.length === 0) {
      vscode.window.showWarningMessage(
        'cstemplate: No template file is open or selected.'
      );
      return undefined;
    }

    if (files.length === 1) { return files[0]; }

    const items = files.map(f => ({
      label: path.basename(f.fsPath),
      description: vscode.workspace.asRelativePath(f),
      uri: f,
    }));

    const picked = await vscode.window.showQuickPick(items, {
      placeHolder: 'Select a template to run',
    });

    return picked?.uri;
  }

  private isTemplateFile(uri: vscode.Uri): boolean {
    return uri.fsPath.endsWith('.template.cs');
  }
}
