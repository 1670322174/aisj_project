import { requestWithAuth } from "../client";

type Raw = Record<string, unknown>;
type Envelope<T> = { data?: T; Data?: T };

export interface AssistantBrief {
  roomType: string;
  area: number | null;
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
}

export interface AssistantChatResponse {
  message: AssistantMessage;
  brief: AssistantBrief;
  proposedAction: AssistantAction | null;
}

export interface AssistantGenerationResponse {
  actionId: number;
  jobId: string;
  status: string;
  workflowCode: string;
}

const emptyBrief = (): AssistantBrief => ({
  roomType: "",
  area: null,
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
    area: pick<number | null>(raw, ["area", "Area"], null),
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
  return {
    messageId: Number(pick(raw, ["messageId", "MessageId"], 0)),
    role: pick(raw, ["role", "Role"], "assistant"),
    content: String(pick(raw, ["content", "Content"], "")),
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

function detail(value: unknown): AssistantConversationDetail {
  const raw = object(value);
  return {
    conversation: summary(pick(raw, ["conversation", "Conversation"], {})),
    brief: brief(pick(raw, ["brief", "Brief"], emptyBrief())),
    messages: pick<unknown[]>(raw, ["messages", "Messages"], []).map(message),
    actions: pick<unknown[]>(raw, ["actions", "Actions"], []).map(action),
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
): Promise<AssistantChatResponse> {
  const raw = object(
    unwrap(
      await requestWithAuth(`/assistant/conversations/${id}/messages`, {
        method: "POST",
        body: JSON.stringify({ content, clientRequestId }),
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
  };
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
async function deleteConversation(id: number): Promise<void> {
  await requestWithAuth(`/assistant/conversations/${id}`, { method: "DELETE" });
}

export const assistantApi = {
  createConversation,
  getConversations,
  getConversation,
  updateBinding,
  sendMessage,
  executeGeneration,
  deleteConversation,
};
