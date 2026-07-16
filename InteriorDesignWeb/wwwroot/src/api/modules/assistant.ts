import { requestWithAuth } from "../client";
import { fetchAuthenticatedMedia } from "../media";

type Raw = Record<string, unknown>;
type Envelope<T> = { data?: T; Data?: T };

export interface AssistantBrief {
  roomType: string;
  area: string;
  style: string;
  colors: string[];
  materials: string[];
  requirements: string[];
  lighting: string;
  constraints: string[];
  missingFields: string[];
}

export interface AssistantConversationSummary {
  conversationId: number;
  title: string;
  status: string;
  projectId: number | null;
  projectName: string;
  roomId: number | null;
  roomName: string;
  createdAt: string;
  updatedAt: string;
}

export interface AssistantMessage {
  messageId: number;
  role: "user" | "assistant" | "system";
  content: string;
  structuredDataJson: string;
  modelCode: string;
  createdAt: string;
}

export interface AssistantAction {
  actionId: number;
  messageId: number | null;
  jobId: string;
  status: string;
  generationType: string;
  workflowCode: string;
  prompt: string;
  negativePrompt: string;
  parametersJson: string;
  projectId: number | null;
  roomId: number | null;
  autoAddToProject: boolean;
  createdAt: string;
}

export interface AssistantConversationDetail {
  conversation: AssistantConversationSummary;
  brief: AssistantBrief;
  messages: AssistantMessage[];
  actions: AssistantAction[];
  agentRuns: AssistantAgentRun[];
  agentArtifacts: AssistantAgentArtifact[];
  attachments: AssistantAttachment[];
  rooms: AssistantRoomProgress[];
}

export interface AssistantAttachment {
  attachmentId: number;
  messageId: number | null;
  roomId: number | null;
  fileName: string;
  contentType: string;
  fileSize: number;
  width: number;
  height: number;
  kind: string;
  visionStatus: string;
  createdAt: string;
}

export interface AssistantRoomProgress {
  roomId: number;
  name: string;
  roomType: string;
  status: string;
  orderIndex: number;
  selected: boolean;
}

export interface AssistantChatResponse {
  message: AssistantMessage;
  brief: AssistantBrief;
  proposedAction: AssistantAction | null;
  agentRun: AssistantAgentRun | null;
}

export interface AssistantAgentEvent {
  eventId: number;
  sequence: number;
  agentId: string;
  eventType: string;
  stage: string;
  title: string;
  detail: string;
  dataJson: string;
  createdAt: string;
}

export interface AssistantAgentRun {
  runId: number;
  clientRequestId: string;
  status: string;
  entryAgentId: string;
  currentAgentId: string;
  currentStage: string;
  modelCallCount: number;
  handoffCount: number;
  inputTokens: number;
  outputTokens: number;
  durationMs: number;
  errorCode: string;
  errorMessage: string;
  startedAt: string;
  completedAt: string;
  events: AssistantAgentEvent[];
}

export interface AssistantAgentArtifact {
  artifactId: number;
  runId: number;
  agentId: string;
  artifactType: string;
  version: number;
  status: string;
  title: string;
  contentJson: string;
  createdAt: string;
}

export interface AssistantGenerationResponse {
  actionId: number;
  jobId: string;
  status: string;
  workflowCode: string;
}

export interface AssistantResultEvaluation {
  actionId: number;
  evaluationJson: string;
  run: AssistantAgentRun;
}

const emptyBrief = (): AssistantBrief => ({
  roomType: "",
  area: "",
  style: "",
  colors: [],
  materials: [],
  requirements: [],
  lighting: "",
  constraints: [],
  missingFields: [],
});
const object = (value: unknown): Raw =>
  value && typeof value === "object" ? (value as Raw) : {};
const pick = <T>(raw: Raw, keys: string[], fallback: T): T => {
  for (const key of keys)
    if (raw[key] !== undefined && raw[key] !== null) return raw[key] as T;
  return fallback;
};
const unwrap = <T>(value: unknown): T => {
  const envelope = value as Envelope<T>;
  return (envelope?.data ?? envelope?.Data ?? value) as T;
};
const strings = (value: unknown): string[] =>
  Array.isArray(value) ? value.map(String).filter(Boolean) : [];

function brief(value: unknown): AssistantBrief {
  const raw = object(value);
  return {
    roomType: String(pick(raw, ["roomType", "RoomType"], "")),
    area: String(pick(raw, ["area", "Area"], "")),
    style: String(pick(raw, ["style", "Style"], "")),
    colors: strings(pick(raw, ["colors", "Colors"], [])),
    materials: strings(pick(raw, ["materials", "Materials"], [])),
    requirements: strings(pick(raw, ["requirements", "Requirements"], [])),
    lighting: String(pick(raw, ["lighting", "Lighting"], "")),
    constraints: strings(pick(raw, ["constraints", "Constraints"], [])),
    missingFields: strings(pick(raw, ["missingFields", "MissingFields"], [])),
  };
}

function summary(value: unknown): AssistantConversationSummary {
  const raw = object(value);
  return {
    conversationId: Number(pick(raw, ["conversationId", "ConversationId"], 0)),
    title: String(pick(raw, ["title", "Title"], "新设计对话")),
    status: String(pick(raw, ["status", "Status"], "active")),
    projectId: pick(raw, ["projectId", "ProjectId"], null),
    projectName: String(pick(raw, ["projectName", "ProjectName"], "")),
    roomId: pick(raw, ["roomId", "RoomId"], null),
    roomName: String(pick(raw, ["roomName", "RoomName"], "")),
    createdAt: String(pick(raw, ["createdAt", "CreatedAt"], "")),
    updatedAt: String(pick(raw, ["updatedAt", "UpdatedAt"], "")),
  };
}

function message(value: unknown): AssistantMessage {
  const raw = object(value);
  const rawContent = String(pick(raw, ["content", "Content"], ""));
  let content = rawContent;
  if (rawContent.trimStart().startsWith("{")) {
    try {
      const parsed = object(JSON.parse(rawContent));
      const assistantText = pick(parsed, ["assistantText", "AssistantText"], "");
      if (assistantText) content = String(assistantText);
    } catch {
      // A normal message may legitimately start with a brace. Keep it unchanged.
    }
  }
  return {
    messageId: Number(pick(raw, ["messageId", "MessageId"], 0)),
    role: pick(raw, ["role", "Role"], "assistant"),
    content,
    structuredDataJson: String(
      pick(raw, ["structuredDataJson", "StructuredDataJson"], ""),
    ),
    modelCode: String(pick(raw, ["modelCode", "ModelCode"], "")),
    createdAt: String(pick(raw, ["createdAt", "CreatedAt"], "")),
  };
}

function action(value: unknown): AssistantAction {
  const raw = object(value);
  return {
    actionId: Number(pick(raw, ["actionId", "ActionId"], 0)),
    messageId: pick(raw, ["messageId", "MessageId"], null),
    jobId: String(pick(raw, ["jobId", "JobId"], "")),
    status: String(pick(raw, ["status", "Status"], "proposed")),
    generationType: String(
      pick(raw, ["generationType", "GenerationType"], "text_to_image"),
    ),
    workflowCode: String(pick(raw, ["workflowCode", "WorkflowCode"], "")),
    prompt: String(pick(raw, ["prompt", "Prompt"], "")),
    negativePrompt: String(pick(raw, ["negativePrompt", "NegativePrompt"], "")),
    parametersJson: String(
      pick(raw, ["parametersJson", "ParametersJson"], "{}"),
    ),
    projectId: pick(raw, ["projectId", "ProjectId"], null),
    roomId: pick(raw, ["roomId", "RoomId"], null),
    autoAddToProject: Boolean(
      pick(raw, ["autoAddToProject", "AutoAddToProject"], true),
    ),
    createdAt: String(pick(raw, ["createdAt", "CreatedAt"], "")),
  };
}

function agentEvent(value: unknown): AssistantAgentEvent {
  const raw = object(value);
  return {
    eventId: Number(pick(raw, ["eventId", "EventId"], 0)),
    sequence: Number(pick(raw, ["sequence", "Sequence"], 0)),
    agentId: String(pick(raw, ["agentId", "AgentId"], "")),
    eventType: String(pick(raw, ["eventType", "EventType"], "")),
    stage: String(pick(raw, ["stage", "Stage"], "")),
    title: String(pick(raw, ["title", "Title"], "")),
    detail: String(pick(raw, ["detail", "Detail"], "")),
    dataJson: String(pick(raw, ["dataJson", "DataJson"], "")),
    createdAt: String(pick(raw, ["createdAt", "CreatedAt"], "")),
  };
}

function agentRun(value: unknown): AssistantAgentRun {
  const raw = object(value);
  return {
    runId: Number(pick(raw, ["runId", "RunId"], 0)),
    clientRequestId: String(pick(raw, ["clientRequestId", "ClientRequestId"], "")),
    status: String(pick(raw, ["status", "Status"], "")),
    entryAgentId: String(pick(raw, ["entryAgentId", "EntryAgentId"], "")),
    currentAgentId: String(pick(raw, ["currentAgentId", "CurrentAgentId"], "")),
    currentStage: String(pick(raw, ["currentStage", "CurrentStage"], "")),
    modelCallCount: Number(pick(raw, ["modelCallCount", "ModelCallCount"], 0)),
    handoffCount: Number(pick(raw, ["handoffCount", "HandoffCount"], 0)),
    inputTokens: Number(pick(raw, ["inputTokens", "InputTokens"], 0)),
    outputTokens: Number(pick(raw, ["outputTokens", "OutputTokens"], 0)),
    durationMs: Number(pick(raw, ["durationMs", "DurationMs"], 0)),
    errorCode: String(pick(raw, ["errorCode", "ErrorCode"], "")),
    errorMessage: String(pick(raw, ["errorMessage", "ErrorMessage"], "")),
    startedAt: String(pick(raw, ["startedAt", "StartedAt"], "")),
    completedAt: String(pick(raw, ["completedAt", "CompletedAt"], "")),
    events: pick<unknown[]>(raw, ["events", "Events"], []).map(agentEvent),
  };
}

function agentArtifact(value: unknown): AssistantAgentArtifact {
  const raw = object(value);
  return {
    artifactId: Number(pick(raw, ["artifactId", "ArtifactId"], 0)),
    runId: Number(pick(raw, ["runId", "RunId"], 0)),
    agentId: String(pick(raw, ["agentId", "AgentId"], "")),
    artifactType: String(pick(raw, ["artifactType", "ArtifactType"], "")),
    version: Number(pick(raw, ["version", "Version"], 0)),
    status: String(pick(raw, ["status", "Status"], "")),
    title: String(pick(raw, ["title", "Title"], "")),
    contentJson: String(pick(raw, ["contentJson", "ContentJson"], "{}")),
    createdAt: String(pick(raw, ["createdAt", "CreatedAt"], "")),
  };
}

function attachment(value: unknown): AssistantAttachment {
  const raw = object(value);
  return {
    attachmentId: Number(pick(raw, ["attachmentId", "AttachmentId"], 0)),
    messageId: pick(raw, ["messageId", "MessageId"], null),
    roomId: pick(raw, ["roomId", "RoomId"], null),
    fileName: String(pick(raw, ["fileName", "FileName"], "图片")),
    contentType: String(pick(raw, ["contentType", "ContentType"], "image/jpeg")),
    fileSize: Number(pick(raw, ["fileSize", "FileSize"], 0)),
    width: Number(pick(raw, ["width", "Width"], 0)),
    height: Number(pick(raw, ["height", "Height"], 0)),
    kind: String(pick(raw, ["kind", "Kind"], "unclassified")),
    visionStatus: String(pick(raw, ["visionStatus", "VisionStatus"], "pending")),
    createdAt: String(pick(raw, ["createdAt", "CreatedAt"], "")),
  };
}

function roomProgress(value: unknown): AssistantRoomProgress {
  const raw = object(value);
  return {
    roomId: Number(pick(raw, ["roomId", "RoomId"], 0)),
    name: String(pick(raw, ["name", "Name"], "房间")),
    roomType: String(pick(raw, ["roomType", "RoomType"], "")),
    status: String(pick(raw, ["status", "Status"], "not_started")),
    orderIndex: Number(pick(raw, ["orderIndex", "OrderIndex"], 0)),
    selected: Boolean(pick(raw, ["selected", "Selected"], false)),
  };
}

function detail(value: unknown): AssistantConversationDetail {
  const raw = object(value);
  return {
    conversation: summary(pick(raw, ["conversation", "Conversation"], {})),
    brief: brief(pick(raw, ["brief", "Brief"], emptyBrief())),
    messages: pick<unknown[]>(raw, ["messages", "Messages"], []).map(message),
    actions: pick<unknown[]>(raw, ["actions", "Actions"], []).map(action),
    agentRuns: pick<unknown[]>(raw, ["agentRuns", "AgentRuns"], []).map(agentRun),
    agentArtifacts: pick<unknown[]>(raw, ["agentArtifacts", "AgentArtifacts"], []).map(agentArtifact),
    attachments: pick<unknown[]>(raw, ["attachments", "Attachments"], []).map(attachment),
    rooms: pick<unknown[]>(raw, ["rooms", "Rooms"], []).map(roomProgress),
  };
}

async function createConversation(
  input: {
    title?: string;
    projectId?: number | null;
    roomId?: number | null;
  } = {},
): Promise<AssistantConversationDetail> {
  return detail(
    unwrap(
      await requestWithAuth("/assistant/conversations", {
        method: "POST",
        body: JSON.stringify(input),
      }),
    ),
  );
}
async function getConversations(): Promise<AssistantConversationSummary[]> {
  return (
    unwrap<unknown[]>(await requestWithAuth("/assistant/conversations")) ?? []
  ).map(summary);
}
async function getConversation(
  id: number,
): Promise<AssistantConversationDetail> {
  return detail(
    unwrap(await requestWithAuth(`/assistant/conversations/${id}`)),
  );
}
async function updateBinding(
  id: number,
  projectId: number | null,
  roomId: number | null,
): Promise<AssistantConversationSummary> {
  return summary(
    unwrap(
      await requestWithAuth(`/assistant/conversations/${id}/binding`, {
        method: "PATCH",
        body: JSON.stringify({ projectId, roomId }),
      }),
    ),
  );
}
async function sendMessage(
  id: number,
  content: string,
  clientRequestId: string,
  attachmentIds: number[] = [],
): Promise<AssistantChatResponse> {
  const raw = object(
    unwrap(
      await requestWithAuth(`/assistant/conversations/${id}/messages`, {
        method: "POST",
        body: JSON.stringify({ content, clientRequestId, attachmentIds }),
      }),
    ),
  );
  const proposed = pick<unknown | null>(
    raw,
    ["proposedAction", "ProposedAction"],
    null,
  );
  return {
    message: message(pick(raw, ["message", "Message"], {})),
    brief: brief(pick(raw, ["brief", "Brief"], {})),
    proposedAction: proposed ? action(proposed) : null,
    agentRun: pick(raw, ["agentRun", "AgentRun"], null) ? agentRun(pick(raw, ["agentRun", "AgentRun"], {})) : null,
  };
}
async function uploadAttachment(
  conversationId: number,
  file: File,
  roomId: number | null,
): Promise<AssistantAttachment> {
  const body = new FormData();
  body.append("file", file);
  if (roomId) body.append("roomId", String(roomId));
  return attachment(unwrap(await requestWithAuth(
    `/assistant/conversations/${conversationId}/attachments`,
    { method: "POST", body },
  )));
}
async function deleteAttachment(conversationId: number, attachmentId: number): Promise<void> {
  await requestWithAuth(
    `/assistant/conversations/${conversationId}/attachments/${attachmentId}`,
    { method: "DELETE" },
  );
}
async function fetchAttachmentMedia(
  conversationId: number,
  attachmentId: number,
  type: "thumbnail" | "original" = "thumbnail",
): Promise<string> {
  return fetchAuthenticatedMedia(
    `/assistant/conversations/${conversationId}/attachments/${attachmentId}/file?type=${type}`,
    `assistant-attachment:${conversationId}:${attachmentId}:${type}`,
  );
}
async function getAgentRun(id: number, clientRequestId: string): Promise<AssistantAgentRun> {
  return agentRun(unwrap(await requestWithAuth(`/assistant/conversations/${id}/agent-runs/by-request/${encodeURIComponent(clientRequestId)}`)));
}
async function executeGeneration(
  conversationId: number,
  actionId: number,
  input: {
    prompt?: string;
    negativePrompt?: string;
    parameters?: Record<string, unknown>;
    autoAddToProject?: boolean;
  },
): Promise<AssistantGenerationResponse> {
  const raw = object(
    unwrap(
      await requestWithAuth(
        `/assistant/conversations/${conversationId}/actions/${actionId}/execute`,
        { method: "POST", body: JSON.stringify(input) },
      ),
    ),
  );
  return {
    actionId: Number(pick(raw, ["actionId", "ActionId"], actionId)),
    jobId: String(pick(raw, ["jobId", "JobId"], "")),
    status: String(pick(raw, ["status", "Status"], "")),
    workflowCode: String(pick(raw, ["workflowCode", "WorkflowCode"], "")),
  };
}
async function evaluateGeneration(
  conversationId: number,
  actionId: number,
  clientRequestId: string,
): Promise<AssistantResultEvaluation> {
  const raw = object(unwrap(await requestWithAuth(
    `/assistant/conversations/${conversationId}/actions/${actionId}/evaluate`,
    { method: "POST", body: JSON.stringify({ clientRequestId }) },
  )));
  return {
    actionId: Number(pick(raw, ["actionId", "ActionId"], actionId)),
    evaluationJson: String(pick(raw, ["evaluationJson", "EvaluationJson"], "{}")),
    run: agentRun(pick(raw, ["run", "Run"], {})),
  };
}
async function deleteConversation(id: number): Promise<void> {
  await requestWithAuth(`/assistant/conversations/${id}`, { method: "DELETE" });
}

export const assistantApi = {
  createConversation,
  getConversations,
  getConversation,
  updateBinding,
  sendMessage,
  uploadAttachment,
  deleteAttachment,
  fetchAttachmentMedia,
  getAgentRun,
  executeGeneration,
  evaluateGeneration,
  deleteConversation,
};
