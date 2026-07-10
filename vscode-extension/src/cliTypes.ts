/**
 * Mirrors the JSON output schema of `cstemplate run --json` and `cstemplate check --json`.
 * Keep in sync with CliOutput / CliDiagnostic in CsTemplate.Cli/OutputReporter.cs.
 */

export type CliStatus =
  | 'success'
  | 'compilationError'
  | 'executionError'
  | 'error';

export interface CliDiagnostic {
  severity: 'error' | 'warning' | 'info';
  message: string;
  filePath?: string;
  line: number;
  column: number;
}

export interface CliOutput {
  status: CliStatus;
  templateName?: string;
  outputRoot?: string;
  generatedFiles?: string[];
  fileCount?: number;
  message?: string;
  innerExceptionType?: string;
  innerMessage?: string;
  stackTrace?: string;
  diagnostics?: CliDiagnostic[];
}

/** Exit codes returned by the cstemplate CLI process. */
export const enum ExitCode {
  Success         = 0,
  CompilationError = 1,
  ExecutionError  = 2,
  InputError      = 3,
  UnexpectedError = 99,
}
