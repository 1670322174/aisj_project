import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  Bot,
  Check,
  ChevronRight,
  Image as ImageIcon,
  Loader2,
  MessageSquarePlus,
  PanelLeft,
  Send,
  Sparkles,
  Trash2,
  WandSparkles,
} from "lucide-react";
import { ApiRequestError } from "@/api/client";
import {
  assistantApi,
  type AssistantAction,
  type AssistantConversationDetail,
  type AssistantConversationSummary,
} from "@/api/modules/assistant";
import { aiApi, type AIJob, type AIJobResult } from "@/api/modules/ai";
import { projectsApi, type Project, type Room } from "@/api/modules/projects";
import ImageLightbox, { type ImageItem } from "@/components/ImageLightbox";
import { cn } from "@/utils/cn";

const panel =
  "rounded-2xl border border-[var(--border-default)] bg-[var(--bg-card)] shadow-[var(--shadow-card)]";
const input =
  "w-full rounded-xl border border-[var(--border-default)] bg-[var(--bg-input)] px-3.5 py-2.5 text-sm text-[var(--text-primary)] placeholder:text-[var(--text-placeholder)] focus:border-[var(--accent-border)] focus:outline-none";
const secondary =
  "inline-flex items-center justify-center gap-2 rounded-xl border border-[var(--border-default)] bg-[var(--bg-input)] px-3 py-2 text-xs font-medium text-[var(--text-secondary)] hover:border-[var(--border-strong)] hover:text-[var(--text-primary)] disabled:opacity-50";
const blankDetail = (): AssistantConversationDetail => ({
  conversation: {
    conversationId: 0,
    title: "",
    status: "active",
    projectId: null,
    projectName: "",
    roomId: null,
    roomName: "",
    createdAt: "",
    updatedAt: "",
  },
  brief: {
    roomType: "",
    area: null,
    style: "",
    colors: [],
    materials: [],
    requirements: [],
    lighting: "",
    constraints: [],
    missingFields: [],
  },
  messages: [],
  actions: [],
  agentRuns: [],
  agentArtifacts: [],
});
const makeRequestId = () =>
  globalThis.crypto?.randomUUID?.().replaceAll("-", "") ??
  `${Date.now()}${Math.random().toString(16).slice(2)}`;
const isTerminal = (status: string) =>
  ["succeeded", "failed", "cancelled", "timeout"].includes(
    status.toLowerCase(),
  );

function formatAssistantError(reason: unknown, fallback: string) {
  if (!(reason instanceof Error)) return fallback;
  if (!(reason instanceof ApiRequestError)) return reason.message || fallback;
  const lines = [reason.message || fallback];
  if (reason.stage) lines.push(`失败阶段：${reason.stage}`);
  if (reason.reason) lines.push(`定位原因：${reason.reason}`);
  if (reason.hint) lines.push(`处理建议：${reason.hint}`);
  if (reason.requestId) lines.push(`请求 ID：${reason.requestId}`);
  if (reason.upstreamRequestId) lines.push(`上游请求 ID：${reason.upstreamRequestId}`);
  if (reason.retryable) lines.push("该问题可以稍后重试。");
  return lines.join("\n");
}

function BriefRow({
  label,
  value,
}: {
  label: string;
  value: string | string[];
}) {
  const values = Array.isArray(value) ? value : value ? [value] : [];
  return (
    <div className="grid grid-cols-[64px_1fr] gap-2 text-xs">
      <span className="text-[var(--text-tertiary)]">{label}</span>
      <div className="flex flex-wrap gap-1.5">
        {values.length ? (
          values.map((item) => (
            <span
              key={item}
              className="rounded-lg border border-[var(--border-subtle)] bg-[var(--bg-input)] px-2 py-1 text-[var(--text-secondary)]"
            >
              {item}
            </span>
          ))
        ) : (
          <span className="text-[var(--text-placeholder)]">待补充</span>
        )}
      </div>
    </div>
  );
}

function GenerationCard({
  action,
  bound,
  executing,
  onExecute,
}: {
  action: AssistantAction;
  bound: boolean;
  executing: boolean;
  onExecute: (prompt: string, negativePrompt: string, autoAdd: boolean) => void;
}) {
  const [prompt, setPrompt] = useState(action.prompt);
  const [negativePrompt, setNegativePrompt] = useState(action.negativePrompt);
  const [autoAdd, setAutoAdd] = useState(action.autoAddToProject);
  useEffect(() => {
    setPrompt(action.prompt);
    setNegativePrompt(action.negativePrompt);
    setAutoAdd(action.autoAddToProject);
  }, [action]);
  return (
    <section className={cn(panel, "p-4")}>
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="flex items-center gap-2 text-sm font-semibold text-[var(--text-primary)]">
            <WandSparkles size={16} className="text-[var(--accent)]" />
            生成建议
          </p>
          <p className="mt-1 text-[11px] text-[var(--text-tertiary)]">
            确认前可以修改提示词，不会自动产生费用。
          </p>
        </div>
        <span className="rounded-full border border-amber-400/20 bg-amber-400/10 px-2 py-1 text-[10px] text-amber-300">
          待确认
        </span>
      </div>
      <label className="mt-4 block text-[11px] text-[var(--text-secondary)]">
        专业提示词
        <textarea
          value={prompt}
          onChange={(event) => setPrompt(event.target.value)}
          rows={7}
          className={cn(input, "mt-1.5 resize-y text-xs leading-5")}
        />
      </label>
      <label className="mt-3 block text-[11px] text-[var(--text-secondary)]">
        补充负面提示词
        <textarea
          value={negativePrompt}
          onChange={(event) => setNegativePrompt(event.target.value)}
          rows={3}
          className={cn(input, "mt-1.5 resize-y text-xs leading-5")}
        />
      </label>
      <label className="mt-3 flex items-center gap-2 text-xs text-[var(--text-secondary)]">
        <input
          type="checkbox"
          checked={autoAdd}
          onChange={(event) => setAutoAdd(event.target.checked)}
        />
        生成后自动加入所选方案房间
      </label>
      {!bound && (
        <p className="mt-3 rounded-xl border border-amber-400/20 bg-amber-400/8 p-3 text-[11px] text-amber-200">
          请先在“当前方案”中选择方案和房间。
        </p>
      )}
      <button
        disabled={!bound || executing || !prompt.trim()}
        onClick={() => onExecute(prompt, negativePrompt, autoAdd)}
        className="mt-4 flex w-full items-center justify-center gap-2 rounded-xl bg-[var(--accent)] px-4 py-2.5 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-45"
      >
        {executing ? (
          <Loader2 size={16} className="animate-spin" />
        ) : (
          <Sparkles size={16} />
        )}
        {executing ? "正在创建任务…" : "确认生成效果图"}
      </button>
    </section>
  );
}

export default function AIAssistantWorkspacePage() {
  const { conversationId } = useParams();
  const navigate = useNavigate();
  const activeId = Number(conversationId || 0);
  const [conversations, setConversations] = useState<
    AssistantConversationSummary[]
  >([]);
  const [detail, setDetail] =
    useState<AssistantConversationDetail>(blankDetail());
  const [projects, setProjects] = useState<Project[]>([]);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [message, setMessage] = useState("");
  const [pendingUser, setPendingUser] = useState("");
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [executing, setExecuting] = useState(false);
  const [error, setError] = useState("");
  const [job, setJob] = useState<AIJob | null>(null);
  const [results, setResults] = useState<
    Array<AIJobResult & { preview: string; original?: string }>
  >([]);
  const [lightboxIndex, setLightboxIndex] = useState(-1);
  const messagesEnd = useRef<HTMLDivElement>(null);

  const refreshConversations = useCallback(
    () => assistantApi.getConversations().then(setConversations),
    [],
  );
  const loadDetail = useCallback(async (id: number) => {
    if (!id) {
      setDetail(blankDetail());
      setLoading(false);
      return;
    }
    setLoading(true);
    setError("");
    try {
      const value = await assistantApi.getConversation(id);
      setDetail(value);
      const latestJob = [...value.actions].reverse().find((item) => item.jobId);
      if (latestJob?.jobId) setJob(await aiApi.getJob(latestJob.jobId));
      else {
        setJob(null);
        setResults([]);
      }
    } catch (reason) {
      setError(formatAssistantError(reason, "对话加载失败"));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refreshConversations();
    projectsApi
      .getUserProjects()
      .then(setProjects)
      .catch(() => setProjects([]));
  }, [refreshConversations]);
  useEffect(() => {
    void loadDetail(activeId);
  }, [activeId, loadDetail]);
  useEffect(() => {
    const projectId = detail.conversation.projectId;
    if (!projectId) {
      setRooms([]);
      return;
    }
    projectsApi.getProjectRooms(String(projectId)).then(setRooms);
  }, [detail.conversation.projectId]);
  useEffect(() => {
    messagesEnd.current?.scrollIntoView({ behavior: "smooth" });
  }, [detail.messages.length, pendingUser, sending]);

  useEffect(() => {
    const jobId = job?.jobId;
    if (!jobId) return;
    let active = true;
    let timer: number | undefined;
    const poll = async () => {
      try {
        const current = await aiApi.getJob(jobId);
        if (!active) return;
        setJob(current);
        if (current.status.toLowerCase() === "succeeded") {
          const raw = await aiApi.getJobResults(jobId);
          const withMedia = await Promise.all(
            raw.map(async (item) => ({
              ...item,
              preview: await aiApi.fetchJobResultMedia(
                jobId,
                item.aiImageID,
                "thumbnail",
              ),
            })),
          );
          if (active) setResults(withMedia);
        }
        if (!isTerminal(current.status)) timer = window.setTimeout(poll, 2500);
      } catch (reason) {
        if (active)
          setError(formatAssistantError(reason, "任务状态获取失败"));
      }
    };
    void poll();
    return () => {
      active = false;
      if (timer) window.clearTimeout(timer);
    };
  }, [job?.jobId]);

  const proposedAction = useMemo(
    () =>
      [...detail.actions]
        .reverse()
        .find(
          (item) => !item.jobId && ["proposed", "failed"].includes(item.status),
        ),
    [detail.actions],
  );
  const bound = Boolean(
    detail.conversation.projectId && detail.conversation.roomId,
  );
  const lightboxImages: ImageItem[] = results.map((item) => ({
    id: String(item.aiImageID),
    src: item.original ?? item.preview,
    label: "AI 设计助手生成结果",
    source: "ai",
  }));

  async function openResult(index: number) {
    const item = results[index];
    if (!item || !job?.jobId) return;
    if (!item.original) {
      try {
        const original = await aiApi.fetchJobResultMedia(
          job.jobId,
          item.aiImageID,
          "original",
        );
        setResults((current) =>
          current.map((value, currentIndex) =>
            currentIndex === index ? { ...value, original } : value,
          ),
        );
      } catch (reason) {
      setError(formatAssistantError(reason, "原图加载失败"));
      }
    }
    setLightboxIndex(index);
  }

  async function createConversation() {
    setError("");
    try {
      const created = await assistantApi.createConversation();
      await refreshConversations();
      navigate(`/app/assistant/${created.conversation.conversationId}`);
    } catch (reason) {
      setError(formatAssistantError(reason, "创建对话失败"));
    }
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    const content = message.trim();
    if (!content || sending) return;
    let id = activeId;
    setSending(true);
    setError("");
    setMessage("");
    setPendingUser(content);
    try {
      if (!id) {
        const created = await assistantApi.createConversation();
        id = created.conversation.conversationId;
        navigate(`/app/assistant/${id}`, { replace: true });
      }
      await assistantApi.sendMessage(id, content, makeRequestId());
      await Promise.all([loadDetail(id), refreshConversations()]);
    } catch (reason) {
      setError(formatAssistantError(reason, "消息发送失败"));
      setMessage(content);
    } finally {
      setPendingUser("");
      setSending(false);
    }
  }
  async function bindProject(value: string) {
    if (!activeId) return;
    try {
      await assistantApi.updateBinding(
        activeId,
        value ? Number(value) : null,
        null,
      );
      await loadDetail(activeId);
    } catch (reason) {
      setError(formatAssistantError(reason, "方案绑定失败"));
    }
  }
  async function bindRoom(value: string) {
    if (!activeId) return;
    try {
      await assistantApi.updateBinding(
        activeId,
        detail.conversation.projectId,
        value ? Number(value) : null,
      );
      await loadDetail(activeId);
    } catch (reason) {
      setError(formatAssistantError(reason, "房间绑定失败"));
    }
  }
  async function execute(
    action: AssistantAction,
    prompt: string,
    negativePrompt: string,
    autoAdd: boolean,
  ) {
    if (!activeId) return;
    setExecuting(true);
    setError("");
    try {
      const response = await assistantApi.executeGeneration(
        activeId,
        action.actionId,
        { prompt, negativePrompt, autoAddToProject: autoAdd },
      );
      setJob(await aiApi.getJob(response.jobId));
      await loadDetail(activeId);
    } catch (reason) {
      setError(formatAssistantError(reason, "生成任务提交失败"));
    } finally {
      setExecuting(false);
    }
  }
  async function removeConversation(id: number) {
    if (
      !window.confirm(
        "删除这条设计对话？已经生成的 AI 任务和方案图片不会被删除。",
      )
    )
      return;
    await assistantApi.deleteConversation(id);
    await refreshConversations();
    if (id === activeId) navigate("/app/assistant");
  }

  return (
    <div className="min-h-full bg-[var(--bg-base)] p-3 md:p-5">
      <div className="mx-auto grid max-w-[1800px] gap-4 xl:h-[calc(100vh-2.5rem)] xl:grid-cols-[250px_minmax(460px,1fr)_340px]">
        <aside
          className={cn(
            panel,
            "flex max-h-72 flex-col overflow-hidden xl:max-h-none",
          )}
        >
          <div className="flex items-center justify-between border-b border-[var(--border-subtle)] p-3.5">
            <p className="flex items-center gap-2 text-sm font-semibold text-[var(--text-primary)]">
              <PanelLeft size={16} />
              设计对话
            </p>
            <button
              onClick={() => void createConversation()}
              className="rounded-lg bg-[var(--accent)] p-2 text-white"
            >
              <MessageSquarePlus size={15} />
            </button>
          </div>
          <div className="flex-1 space-y-1 overflow-auto p-2">
            {conversations.length ? (
              conversations.map((item) => (
                <div
                  key={item.conversationId}
                  className={cn(
                    "group flex items-center rounded-xl",
                    item.conversationId === activeId && "bg-[var(--accent)]/10",
                  )}
                >
                  <button
                    onClick={() =>
                      navigate(`/app/assistant/${item.conversationId}`)
                    }
                    className="min-w-0 flex-1 p-2.5 text-left"
                  >
                    <p className="truncate text-xs font-medium text-[var(--text-primary)]">
                      {item.title}
                    </p>
                    <p className="mt-1 truncate text-[10px] text-[var(--text-tertiary)]">
                      {item.projectName
                        ? `${item.projectName}${item.roomName ? ` · ${item.roomName}` : ""}`
                        : "尚未绑定方案"}
                    </p>
                  </button>
                  <button
                    onClick={() => void removeConversation(item.conversationId)}
                    className="mr-2 hidden rounded-lg p-1.5 text-[var(--text-tertiary)] hover:bg-red-400/10 hover:text-red-300 group-hover:block"
                  >
                    <Trash2 size={13} />
                  </button>
                </div>
              ))
            ) : (
              <p className="p-4 text-center text-xs text-[var(--text-tertiary)]">
                还没有设计对话
              </p>
            )}
          </div>
        </aside>
        <main
          className={cn(
            panel,
            "flex min-h-[650px] flex-col overflow-hidden xl:min-h-0",
          )}
        >
          <header className="flex items-center justify-between border-b border-[var(--border-subtle)] px-4 py-3.5">
            <div className="flex items-center gap-3">
              <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-[var(--accent)]/15 text-[var(--accent)]">
                <Bot size={19} />
              </span>
              <div>
                <h1 className="text-sm font-semibold text-[var(--text-primary)]">
                  {detail.conversation.title || "AI 设计助手"}
                </h1>
                <p className="text-[10px] text-[var(--text-tertiary)]">
                  需求梳理 · 提示词优化 · 效果图生成
                </p>
              </div>
            </div>
            {job && (
              <span className="rounded-full border border-[var(--border-default)] bg-[var(--bg-input)] px-2.5 py-1 text-[10px] text-[var(--text-secondary)]">
                任务 {job.status} · {job.progressValue}%
              </span>
            )}
          </header>
          <div className="flex-1 space-y-4 overflow-auto p-4 md:p-6">
            {loading ? (
              <div className="flex h-full items-center justify-center">
                <Loader2 className="animate-spin text-[var(--accent)]" />
              </div>
            ) : detail.messages.length || pendingUser ? (
              <>
                {detail.messages.map((item) => (
                  <div
                    key={item.messageId}
                    className={cn(
                      "flex",
                      item.role === "user" ? "justify-end" : "justify-start",
                    )}
                  >
                    <div
                      className={cn(
                        "max-w-[86%] rounded-2xl px-4 py-3 text-sm leading-6",
                        item.role === "user"
                          ? "rounded-br-md bg-[var(--accent)] text-white"
                          : "rounded-bl-md border border-[var(--border-subtle)] bg-[var(--bg-input)] text-[var(--text-secondary)]",
                      )}
                    >
                      <p className="whitespace-pre-wrap">{item.content}</p>
                    </div>
                  </div>
                ))}
                {pendingUser && (
                  <div className="flex justify-end">
                    <div className="max-w-[86%] rounded-2xl rounded-br-md bg-[var(--accent)] px-4 py-3 text-sm text-white opacity-70">
                      {pendingUser}
                    </div>
                  </div>
                )}
                {sending && (
                  <div className="flex justify-start">
                    <div className="flex items-center gap-2 rounded-2xl rounded-bl-md border border-[var(--border-subtle)] bg-[var(--bg-input)] px-4 py-3 text-xs text-[var(--text-tertiary)]">
                      <Loader2 size={14} className="animate-spin" />
                      正在整理设计需求…
                    </div>
                  </div>
                )}
                <div ref={messagesEnd} />
              </>
            ) : (
              <div className="flex h-full flex-col items-center justify-center text-center">
                <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--accent)]/12 text-[var(--accent)]">
                  <Sparkles size={25} />
                </span>
                <h2 className="mt-5 text-xl font-semibold text-[var(--text-primary)]">
                  描述你想设计的空间
                </h2>
                <p className="mt-2 max-w-md text-sm leading-6 text-[var(--text-tertiary)]">
                  例如：我想设计一个约 30㎡
                  的现代客厅，希望温暖、通透，并有充足收纳。
                </p>
              </div>
            )}
            {results.length > 0 && (
              <section className="grid gap-3 sm:grid-cols-2">
                {results.map((item, index) => (
                  <button
                    key={item.aiImageID}
                    onClick={() => void openResult(index)}
                    className="overflow-hidden rounded-2xl border border-[var(--border-default)] bg-[var(--bg-input)] text-left"
                  >
                    <img
                      src={item.preview}
                      alt="AI 生成结果"
                      className="h-56 w-full object-cover"
                    />
                    <span className="flex items-center gap-2 p-3 text-xs text-[var(--text-secondary)]">
                      <ImageIcon size={14} className="text-[var(--accent)]" />
                      查看生成结果
                    </span>
                  </button>
                ))}
              </section>
            )}
          </div>
          {error && (
            <div className="mx-4 mb-2 whitespace-pre-line rounded-xl border border-red-400/20 bg-red-400/8 px-3 py-2 text-xs leading-5 text-red-300">
              {error}
            </div>
          )}
          <form
            onSubmit={submit}
            className="border-t border-[var(--border-subtle)] p-3.5"
          >
            <div className="flex items-end gap-2">
              <textarea
                value={message}
                onChange={(event) => setMessage(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter" && !event.shiftKey) {
                    event.preventDefault();
                    event.currentTarget.form?.requestSubmit();
                  }
                }}
                rows={2}
                maxLength={4000}
                className={cn(input, "min-h-[58px] resize-none")}
                placeholder="继续描述需求或提出修改意见…"
              />
              <button
                disabled={sending || !message.trim()}
                className="flex h-[58px] w-[58px] shrink-0 items-center justify-center rounded-xl bg-[var(--accent)] text-white disabled:opacity-40"
              >
                {sending ? (
                  <Loader2 size={18} className="animate-spin" />
                ) : (
                  <Send size={18} />
                )}
              </button>
            </div>
            <p className="mt-2 text-[10px] text-[var(--text-tertiary)]">
              Enter 发送，Shift + Enter
              换行。助手只会提出生成建议，确认后才创建任务。
            </p>
          </form>
        </main>
        <aside className="space-y-4 overflow-auto">
          <section className={cn(panel, "p-4")}>
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold text-[var(--text-primary)]">
                当前方案
              </p>
              {bound && (
                <span className="flex items-center gap-1 text-[10px] text-emerald-300">
                  <Check size={12} />
                  已绑定
                </span>
              )}
            </div>
            <div className="mt-4 space-y-3">
              <label className="block text-[11px] text-[var(--text-tertiary)]">
                方案
                <select
                  value={detail.conversation.projectId ?? ""}
                  disabled={!activeId}
                  onChange={(event) => void bindProject(event.target.value)}
                  className={cn(input, "mt-1.5")}
                >
                  <option value="">选择方案</option>
                  {projects.map((item) => (
                    <option key={item.projectID} value={item.projectID}>
                      {item.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block text-[11px] text-[var(--text-tertiary)]">
                房间
                <select
                  value={detail.conversation.roomId ?? ""}
                  disabled={!detail.conversation.projectId}
                  onChange={(event) => void bindRoom(event.target.value)}
                  className={cn(input, "mt-1.5")}
                >
                  <option value="">选择房间</option>
                  {rooms.map((item) => (
                    <option key={item.roomID} value={item.roomID}>
                      {item.name}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <div className="mt-5 space-y-3 border-t border-[var(--border-subtle)] pt-4">
              <BriefRow label="空间" value={detail.brief.roomType} />
              <BriefRow
                label="面积"
                value={detail.brief.area ? `${detail.brief.area}㎡` : ""}
              />
              <BriefRow label="风格" value={detail.brief.style} />
              <BriefRow label="颜色" value={detail.brief.colors} />
              <BriefRow label="材质" value={detail.brief.materials} />
              <BriefRow label="功能" value={detail.brief.requirements} />
              <BriefRow label="照明" value={detail.brief.lighting} />
              <BriefRow label="限制" value={detail.brief.constraints} />
            </div>
            {detail.brief.missingFields.length > 0 && (
              <div className="mt-4 rounded-xl border border-amber-400/20 bg-amber-400/8 p-3">
                <p className="text-[10px] font-semibold text-amber-300">
                  仍需补充
                </p>
                <p className="mt-1 text-[11px] leading-5 text-amber-100/70">
                  {detail.brief.missingFields.join("、")}
                </p>
              </div>
            )}
          </section>
          {detail.agentRuns.length > 0 && (() => {
            const run = detail.agentRuns[detail.agentRuns.length - 1];
            const latestEvent = run.events[run.events.length - 1];
            const failed = run.status.toLowerCase() === "failed";
            return (
              <section className={cn(panel, "p-4", failed && "border-red-400/25")}>
                <div className="flex items-center justify-between gap-2">
                  <p className="text-sm font-semibold text-[var(--text-primary)]">Agent 运行诊断</p>
                  <span className={cn("text-[10px] font-medium", failed ? "text-red-300" : "text-[var(--text-tertiary)]")}>{run.status}</span>
                </div>
                <div className="mt-3 space-y-1.5 text-[11px] leading-5 text-[var(--text-secondary)]">
                  <p>Run ID：{run.runId}</p>
                  <p>当前 Agent：{run.currentAgentId || run.entryAgentId}</p>
                  <p>当前阶段：{run.currentStage || "-"}</p>
                  <p>模型调用：{run.modelCallCount} 次 · 转交 {run.handoffCount} 次</p>
                  {latestEvent && <p>最新事件：{latestEvent.title}</p>}
                </div>
                {failed && (
                  <div className="mt-3 rounded-xl bg-red-400/8 p-3 text-[11px] leading-5 text-red-200">
                    <p>{run.errorMessage || "Agent 运行失败"}</p>
                    {run.errorCode && <p className="mt-1 text-red-300/80">错误代码：{run.errorCode}</p>}
                    <p className="mt-1 text-red-300/80">客户端请求 ID：{run.clientRequestId}</p>
                  </div>
                )}
              </section>
            );
          })()}
          {proposedAction && (
            <GenerationCard
              action={proposedAction}
              bound={bound}
              executing={executing}
              onExecute={(prompt, negative, auto) =>
                void execute(proposedAction, prompt, negative, auto)
              }
            />
          )}
          {job && (
            <section className={cn(panel, "p-4")}>
              <p className="text-sm font-semibold text-[var(--text-primary)]">
                生成任务
              </p>
              <div className="mt-3 h-2 overflow-hidden rounded-full bg-[var(--bg-input)]">
                <div
                  className="h-full rounded-full bg-[var(--accent)] transition-all"
                  style={{ width: `${Math.max(2, job.progressValue)}%` }}
                />
              </div>
              <div className="mt-2 flex justify-between text-[10px] text-[var(--text-tertiary)]">
                <span>{job.status}</span>
                <span>{job.progressValue}%</span>
              </div>
              {job.errorMessage && (
                <p className="mt-3 text-xs text-red-300">{job.errorMessage}</p>
              )}
              <button
                onClick={() =>
                navigate(`/app/generate/text/jobs/${job.jobId}`)
                }
                className={cn(secondary, "mt-3 w-full")}
              >
                进入完整任务页
                <ChevronRight size={13} />
              </button>
            </section>
          )}
        </aside>
      </div>
      <ImageLightbox
        images={lightboxImages}
        currentIndex={lightboxIndex}
        onClose={() => setLightboxIndex(-1)}
        onIndexChange={setLightboxIndex}
      />
    </div>
  );
}
