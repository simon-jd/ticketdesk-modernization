import { TicketActivityRequest } from '../api/models/ticket-activity-request';

/** The workflow activities the API offers (TicketActivitiesApi.Offered), with the legacy button texts. */
export const ACTIVITY_LABELS: Record<string, string> = {
  AddComment: 'Add comment',
  SupplyMoreInfo: 'Supply more info',
  Resolve: 'Resolve',
  RequestMoreInfo: 'Request more info',
  CancelMoreInfo: 'Cancel more info',
  TakeOver: 'Take over',
  Assign: 'Assign',
  ReAssign: 'Reassign',
  Pass: 'Pass',
  GiveUp: 'Give up',
  Close: 'Close',
  ForceClose: 'Force close',
  ReOpen: 'Reopen',
};

/** What the server said after it saved, for the status message. */
export const ACTIVITY_DONE: Record<string, string> = {
  AddComment: 'Comment added.', SupplyMoreInfo: 'Information supplied.', Resolve: 'Ticket resolved.', RequestMoreInfo: 'More information requested.',
  CancelMoreInfo: 'Request for more information cancelled.', TakeOver: 'You took over the ticket.', Assign: 'Ticket assigned.', ReAssign: 'Ticket reassigned.',
  Pass: 'Ticket passed on.', GiveUp: 'You gave up the ticket.', Close: 'Ticket closed.', ForceClose: 'Ticket closed.', ReOpen: 'Ticket reopened.',
};

const COMMENT_REQUIRED = new Set(['AddComment', 'SupplyMoreInfo', 'RequestMoreInfo', 'Resolve', 'ReOpen', 'GiveUp', 'ForceClose']);
const ASSIGNMENT = new Set(['Assign', 'ReAssign', 'Pass']);

export interface ActivityFields {
  commentRequired: boolean;
  assignee: boolean;
  priority: boolean;
  assignToMe: boolean;
  reactivate: boolean;
}

/** The fields of an activity's form (legacy activity partials). */
export function fieldsFor(activity: string): ActivityFields {
  return {
    commentRequired: COMMENT_REQUIRED.has(activity),
    assignee: ASSIGNMENT.has(activity),
    priority: ASSIGNMENT.has(activity) || activity === 'TakeOver',
    assignToMe: activity === 'ReOpen',
    reactivate: activity === 'SupplyMoreInfo',
  };
}

export interface ActivityValues {
  comment: string;
  assignedTo: string;
  priority: string;
  assignToMe: boolean;
  reactivate: boolean;
}

export function validateActivity(activity: string, v: ActivityValues): Partial<Record<'comment' | 'assignedTo', string>> {
  const f = fieldsFor(activity);
  const e: Partial<Record<'comment' | 'assignedTo', string>> = {};
  if (f.commentRequired && !v.comment.trim()) e.comment = 'Enter a comment.';
  if (f.assignee && !v.assignedTo) e.assignedTo = 'Choose who gets the ticket.';
  return e;
}

export function activityRequest(activity: string, v: ActivityValues): TicketActivityRequest {
  const f = fieldsFor(activity);
  const r: TicketActivityRequest = { activity, comment: v.comment.trim() ? v.comment : null };
  if (f.assignee) r.assignedTo = v.assignedTo;
  if (f.priority && v.priority) r.priority = v.priority;
  if (f.assignToMe) r.assignToMe = v.assignToMe;
  if (f.reactivate) r.reactivate = v.reactivate;
  return r;
}
