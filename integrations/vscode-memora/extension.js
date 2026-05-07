const vscode = require('vscode');

const artifactTypeNames = [
  'charter',
  'plan',
  'decision',
  'constraint',
  'question',
  'outcome',
  'repo_structure',
  'session_summary'
];

const artifactStatusNames = [
  'proposed',
  'draft',
  'approved',
  'superseded',
  'deprecated'
];

function activate(context) {
  const provider = new ReviewInboxProvider();
  const treeView = vscode.window.createTreeView('memoraReviewInbox', {
    treeDataProvider: provider,
    showCollapseAll: false
  });

  context.subscriptions.push(
    treeView,
    vscode.commands.registerCommand('memoraReviewInbox.refresh', () => provider.refresh()),
    vscode.commands.registerCommand('memoraReviewInbox.openPreview', item => openPreview(item)),
    vscode.commands.registerCommand('memoraReviewInbox.openFile', item => openFile(item)),
    vscode.commands.registerCommand('memoraReviewInbox.approve', item => applyDecision(item, 'approve', provider)),
    vscode.commands.registerCommand('memoraReviewInbox.reject', item => applyDecision(item, 'reject', provider))
  );
}

function deactivate() {}

class ReviewInboxProvider {
  constructor() {
    this.emitter = new vscode.EventEmitter();
    this.onDidChangeTreeData = this.emitter.event;
  }

  refresh() {
    this.emitter.fire();
  }

  getTreeItem(item) {
    return item;
  }

  async getChildren() {
    const projectId = getProjectId();
    if (!projectId) {
      return [new MessageTreeItem('Set memora.projectId to show the review inbox.')];
    }

    try {
      const payload = await fetchJson(`/api/projects/${encodeURIComponent(projectId)}/review/inbox`);
      const errors = Array.isArray(payload.errors) ? payload.errors : [];
      if (errors.length > 0) {
        return errors.map(error => new MessageTreeItem(`${error.code}: ${error.message}`));
      }

      const items = Array.isArray(payload.items) ? payload.items : [];
      if (items.length === 0) {
        return [new MessageTreeItem('No draft or proposed artifacts are waiting for review.')];
      }

      return items.map(item => new ReviewInboxTreeItem(item));
    } catch (error) {
      return [new MessageTreeItem(error instanceof Error ? error.message : String(error))];
    }
  }
}

class ReviewInboxTreeItem extends vscode.TreeItem {
  constructor(payload) {
    const title = typeof payload.title === 'string' && payload.title.trim()
      ? payload.title.trim()
      : payload.artifactId;
    super(`${payload.artifactId} r${payload.revision}: ${title}`, vscode.TreeItemCollapsibleState.None);
    this.payload = payload;
    this.contextValue = 'memoraReviewInboxItem';
    this.description = `${formatArtifactType(payload.artifactType)} - ${formatArtifactStatus(payload.status)}`;
    this.tooltip = buildTooltip(payload);
    this.iconPath = new vscode.ThemeIcon(isProposed(payload.status) ? 'git-pull-request' : 'edit');
    this.command = {
      command: 'memoraReviewInbox.openPreview',
      title: 'Open Preview',
      arguments: [this]
    };
  }
}

class MessageTreeItem extends vscode.TreeItem {
  constructor(message) {
    super(message, vscode.TreeItemCollapsibleState.None);
    this.contextValue = 'memoraReviewInboxMessage';
    this.iconPath = new vscode.ThemeIcon('info');
  }
}

async function openPreview(treeItem) {
  const item = unwrapItem(treeItem);
  if (!item) {
    return;
  }

  const projectId = getProjectId();
  if (!projectId) {
    vscode.window.showWarningMessage('Set memora.projectId before opening Memora previews.');
    return;
  }

  try {
    const query = new URLSearchParams({ path: item.relativePath || '' });
    const payload = await fetchJson(`/api/projects/${encodeURIComponent(projectId)}/review/preview?${query}`);
    const errors = Array.isArray(payload.errors) ? payload.errors : [];
    if (errors.length > 0) {
      vscode.window.showErrorMessage(errors.map(error => `${error.code}: ${error.message}`).join('; '));
      return;
    }

    const previewItem = payload.item || item;
    const document = await vscode.workspace.openTextDocument({
      language: 'markdown',
      content: renderPreview(previewItem, payload)
    });
    await vscode.window.showTextDocument(document, { preview: true });
  } catch (error) {
    vscode.window.showErrorMessage(error instanceof Error ? error.message : String(error));
  }
}

async function openFile(treeItem) {
  const item = unwrapItem(treeItem);
  if (!item || !item.filePath) {
    return;
  }

  try {
    const document = await vscode.workspace.openTextDocument(vscode.Uri.file(item.filePath));
    await vscode.window.showTextDocument(document, { preview: false });
  } catch (error) {
    vscode.window.showErrorMessage(error instanceof Error ? error.message : String(error));
  }
}

async function applyDecision(treeItem, decision, provider) {
  const item = unwrapItem(treeItem);
  if (!item) {
    return;
  }

  const projectId = getProjectId();
  if (!projectId) {
    vscode.window.showWarningMessage('Set memora.projectId before applying Memora review decisions.');
    return;
  }

  const label = decision === 'approve' ? 'Approve' : 'Reject';
  const confirmed = await vscode.window.showWarningMessage(
    `${label} ${item.artifactId} revision ${item.revision}?`,
    { modal: true },
    label
  );
  if (confirmed !== label) {
    return;
  }

  try {
    const payload = await postJson(`/api/projects/${encodeURIComponent(projectId)}/review/decisions`, {
      relativePath: item.relativePath || '',
      decision
    });
    const errors = Array.isArray(payload.errors) ? payload.errors : [];
    if (errors.length > 0) {
      vscode.window.showErrorMessage(errors.map(error => `${error.code}: ${error.message}`).join('; '));
      return;
    }

    vscode.window.showInformationMessage(payload.message || `${label} decision persisted by Memora.`);
    provider.refresh();
  } catch (error) {
    vscode.window.showErrorMessage(error instanceof Error ? error.message : String(error));
  }
}

async function fetchJson(path) {
  const baseUrl = getBaseUrl();
  const response = await fetch(`${baseUrl}${path}`);
  const text = await response.text();
  const payload = text ? JSON.parse(text) : {};

  if (!response.ok) {
    const errors = Array.isArray(payload.errors) ? payload.errors : [];
    const message = errors.length > 0
      ? errors.map(error => `${error.code}: ${error.message}`).join('; ')
      : `Memora request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return payload;
}

async function postJson(path, body) {
  const baseUrl = getBaseUrl();
  const response = await fetch(`${baseUrl}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(body)
  });
  const text = await response.text();
  const payload = text ? JSON.parse(text) : {};

  if (!response.ok) {
    const errors = Array.isArray(payload.errors) ? payload.errors : [];
    const message = errors.length > 0
      ? errors.map(error => `${error.code}: ${error.message}`).join('; ')
      : `Memora request failed with HTTP ${response.status}.`;
    throw new Error(message);
  }

  return payload;
}

function renderPreview(item, payload) {
  const lines = [
    `# ${item.artifactId} r${item.revision}: ${item.title}`,
    '',
    `Type: ${formatArtifactType(item.artifactType)}`,
    `Status: ${formatArtifactStatus(item.status)}`,
    `Validation: ${item.validationState || 'unknown'}`,
    `Source: ${item.relativePath || 'unknown'}`,
    `Provenance: ${item.provenance || 'unknown'}`,
    `Reason: ${item.reason || 'unknown'}`,
    ''
  ];

  const body = typeof payload.body === 'string' ? payload.body.trim() : '';
  if (body) {
    lines.push('---', '', body);
  }

  const sections = payload.sections && typeof payload.sections === 'object'
    ? Object.entries(payload.sections)
    : [];
  if (!body && sections.length > 0) {
    lines.push('---', '');
    for (const [heading, content] of sections) {
      lines.push(`## ${heading}`, String(content), '');
    }
  }

  return lines.join('\n');
}

function buildTooltip(item) {
  const tooltip = new vscode.MarkdownString(undefined, true);
  tooltip.appendMarkdown(`**${item.artifactId} r${item.revision}**\n\n`);
  tooltip.appendMarkdown(`- Type: ${formatArtifactType(item.artifactType)}\n`);
  tooltip.appendMarkdown(`- Status: ${formatArtifactStatus(item.status)}\n`);
  tooltip.appendMarkdown(`- Validation: ${item.validationState || 'unknown'}\n`);
  tooltip.appendMarkdown(`- Source: ${item.relativePath || 'unknown'}\n`);
  tooltip.appendMarkdown(`- Provenance: ${item.provenance || 'unknown'}\n`);
  tooltip.appendMarkdown(`- Reason: ${item.reason || 'unknown'}\n`);
  return tooltip;
}

function unwrapItem(treeItem) {
  if (treeItem instanceof ReviewInboxTreeItem) {
    return treeItem.payload;
  }

  return treeItem && typeof treeItem === 'object' ? treeItem : null;
}

function getBaseUrl() {
  const configured = vscode.workspace.getConfiguration('memora').get('apiBaseUrl');
  return String(configured || 'http://127.0.0.1:5081').replace(/\/+$/, '');
}

function getProjectId() {
  const configured = vscode.workspace.getConfiguration('memora').get('projectId');
  return String(configured || '').trim();
}

function formatArtifactType(value) {
  return formatEnum(value, artifactTypeNames);
}

function formatArtifactStatus(value) {
  return formatEnum(value, artifactStatusNames);
}

function formatEnum(value, names) {
  if (typeof value === 'number' && Number.isInteger(value) && value >= 0 && value < names.length) {
    return names[value];
  }

  if (typeof value === 'string' && value.trim()) {
    return value
      .trim()
      .replace(/([a-z0-9])([A-Z])/g, '$1_$2')
      .replace(/[\s-]+/g, '_')
      .toLowerCase();
  }

  return 'unknown';
}

function isProposed(value) {
  return formatArtifactStatus(value) === 'proposed';
}

module.exports = {
  activate,
  deactivate
};
