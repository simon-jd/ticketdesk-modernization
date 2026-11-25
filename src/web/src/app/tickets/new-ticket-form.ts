import { NewTicketRequest } from '../api/models/new-ticket-request';
import { TicketFieldPermissions } from '../api/models/ticket-field-permissions';

/** The new-ticket form's values as the inputs hold them (strings; numbers and dates parsed on submit). */
export interface NewTicketValues {
  projectId: string;
  title: string;
  ticketType: string;
  category: string;
  priority: string;
  affectsCustomer: boolean;
  dueDate: string;
  targetDate: string;
  resolutionDate: string;
  estimatedDuration: string;
  actualDuration: string;
  tagList: string;
  details: string;
  owner: string;
  assignedTo: string;
}

export type FieldErrors = Partial<Record<keyof NewTicketValues, string>>;

/** Labels in the form's order: the error summary lists fields in this order. */
export const FIELD_LABELS: Record<keyof NewTicketValues, string> = {
  projectId: 'Project',
  title: 'Title',
  ticketType: 'Type',
  category: 'Category',
  priority: 'Priority',
  affectsCustomer: 'Affects customer',
  dueDate: 'Due date',
  targetDate: 'Target date',
  resolutionDate: 'Resolution date',
  estimatedDuration: 'Estimated hours',
  actualDuration: 'Actual hours',
  tagList: 'Tags',
  details: 'Details',
  owner: 'Owner',
  assignedTo: 'Assigned to',
};

const MAX = { title: 500, tagList: 100 } as const;

export function emptyValues(projectId: number | null, owner: string): NewTicketValues {
  return {
    projectId: projectId === null ? '' : String(projectId), title: '', ticketType: '', category: '', priority: '', affectsCustomer: false,
    dueDate: '', targetDate: '', resolutionDate: '', estimatedDuration: '', actualDuration: '', tagList: '', details: '', owner, assignedTo: '',
  };
}

function number(value: string): number | null | 'invalid' {
  const v = value.trim();
  if (v === '') return null;
  if (!/^\d+(\.\d+)?$/.test(v)) return 'invalid';
  return Number(v);
}

/** The checks the server also makes, so most mistakes are shown before a round trip (the server stays authoritative). */
export function validate(v: NewTicketValues, multiProject: boolean): FieldErrors {
  const e: FieldErrors = {};
  if (multiProject && !v.projectId) e.projectId = 'Choose a project.';
  if (!v.title.trim()) e.title = 'Enter a title.';
  else if (v.title.trim().length > MAX.title) e.title = `The title can have at most ${MAX.title} characters.`;
  if (!v.ticketType) e.ticketType = 'Choose a type.';
  if (!v.category) e.category = 'Choose a category.';
  if (!v.details.trim()) e.details = 'Describe the problem or request.';
  const tags = v.tagList.split(',').map((t) => t.trim());
  if (v.tagList.trim()) {
    if (tags.some((t) => t.length === 0 || t.length > 100)) e.tagList = 'Separate tags with commas; each tag needs 1 to 100 characters.';
    else if (tags.join(',').length > MAX.tagList) e.tagList = `Tags can have at most ${MAX.tagList} characters in all.`;
  }
  if (number(v.estimatedDuration) === 'invalid') e.estimatedDuration = 'Enter a number of hours, for example 1.5.';
  if (number(v.actualDuration) === 'invalid') e.actualDuration = 'Enter a number of hours, for example 1.5.';
  return e;
}

/** The request body; fields the user may not set are left out (the server would refuse them). Markdown text (isHtml false). */
export function toRequest(v: NewTicketValues, p: TicketFieldPermissions, tempId?: string): NewTicketRequest {
  const n = (s: string) => {
    const x = number(s);
    return x === 'invalid' ? null : x;
  };
  const r: NewTicketRequest = {
    projectId: v.projectId ? Number(v.projectId) : null,
    title: v.title.trim(),
    details: v.details,
    isHtml: false,
    ticketType: v.ticketType,
    category: v.category,
    affectsCustomer: v.affectsCustomer,
    dueDate: v.dueDate || null,
    targetDate: v.targetDate || null,
    resolutionDate: v.resolutionDate || null,
    estimatedDuration: n(v.estimatedDuration),
    actualDuration: n(v.actualDuration),
  };
  if (p.canSetPriority) r.priority = v.priority || null;
  if (p.canSetTags) r.tagList = v.tagList.trim() || null;
  if (p.canSetOwner) r.owner = v.owner || null;
  if (p.canSetAssignedTo) r.assignedTo = v.assignedTo || null;
  if (tempId) r.tempId = tempId;
  return r;
}

/**
 * Server validation errors (keys are the request's camelCase field names; "" for the whole ticket) mapped onto the form's fields;
 * anything without a field goes to `general`.
 */
export function fromServerErrors(errors: Record<string, string[]>): { fields: FieldErrors; general: string[] } {
  const fields: FieldErrors = {};
  const general: string[] = [];
  for (const [key, messages] of Object.entries(errors)) {
    const field = (Object.keys(FIELD_LABELS) as (keyof NewTicketValues)[]).find((f) => f.toLowerCase() === key.toLowerCase());
    if (field) fields[field] = messages.join(' ');
    else general.push(...messages);
  }
  return { fields, general };
}

/** The fields with errors, in form order, for the error summary. */
export function summary(errors: FieldErrors): { field: keyof NewTicketValues; label: string; message: string }[] {
  return (Object.keys(FIELD_LABELS) as (keyof NewTicketValues)[])
    .filter((f) => errors[f])
    .map((f) => ({ field: f, label: FIELD_LABELS[f], message: errors[f]! }));
}
