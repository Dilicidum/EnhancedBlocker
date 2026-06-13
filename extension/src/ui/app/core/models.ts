// Shared domain types mirrored from the backend API contract (TECH_PLAN.md endpoint table).

// Enums travel as strings; the backend binds them case-insensitively, so lowercase is
// fine — EXCEPT multi-word values, which must be the exact PascalCase member name
// (e.g. 'GoodCall', never 'good-call').
export type Outcome = 'Allow' | 'Block' | 'Pending';
export type MatchKind = 'Exact' | 'Domain';
export type RuleKind = 'Block' | 'Allow';
export type FeedbackDecision = 'block' | 'allow';
export type FeedbackSource = 'GoodCall' | 'BadCall';
export type EventType = 'navigate' | 'active' | 'idle';

/** Body sent to POST /decision. */
export interface DecisionContext {
  url: string;
  domain: string;
  title?: string | null;
  text?: string | null;
  focusSessionId?: string | null;
  intent?: string | null;
  now: string; // ISO-8601
}

/** Response from POST /decision. */
export interface TierResult {
  outcome: Outcome;
  tier: string;
  reason: string;
  score?: number | null;
}

/** A single navigation/activity event (POST /events accepts a batch). */
export interface NavEvent {
  ts: string; // ISO-8601
  url: string;
  domain: string;
  title?: string | null;
  tabId: number;
  type: EventType;
  focusSessionId?: string | null;
  durationMs?: number | null;
}

/** Body sent to POST /feedback. */
export interface FeedbackPayload {
  url: string;
  title?: string | null;
  decision: FeedbackDecision;
  /** Optional: the backend infers GoodCall/BadCall from the decision when omitted. */
  source?: FeedbackSource;
}

/** A Tier-0 rule (GET/POST /rules). */
export interface Rule {
  id?: string;
  pattern: string;
  match: MatchKind;
  kind: RuleKind;
  source?: string;
  category?: string | null;
}

/** A managed category in the blocking vocabulary (GET/POST/PUT/DELETE /categories). */
export interface Category {
  id?: string;
  name: string;
}

export interface StartFocusResponse {
  focusSessionId: string;
}
