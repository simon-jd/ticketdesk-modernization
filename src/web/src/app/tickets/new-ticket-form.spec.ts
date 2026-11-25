import { emptyValues, fromServerErrors, summary, toRequest, validate } from './new-ticket-form';

const all = { canSetOwner: true, canSetAssignedTo: true, canSetPriority: true, canSetTags: true };
const none = { canSetOwner: false, canSetAssignedTo: false, canSetPriority: false, canSetTags: false };

function filled() {
  return { ...emptyValues(3, 'me'), title: ' Printer jams ', ticketType: 'Problem', category: 'Hardware', details: 'It **jams**.' };
}

describe('new ticket form', () => {
  it('requires title, type, category, details, and a project when there are several', () => {
    const e = validate(emptyValues(null, 'me'), true);
    expect(Object.keys(e).sort()).toEqual(['category', 'details', 'projectId', 'ticketType', 'title']);
    expect(validate(filled(), true)).toEqual({});
    expect(validate({ ...filled(), projectId: '' }, false)).toEqual({});
  });

  it('checks lengths, tags and numbers like the server', () => {
    const e = validate({ ...filled(), title: 'x'.repeat(501), tagList: 'a, ,b', estimatedDuration: '1,5', actualDuration: '-2' }, false);
    expect(Object.keys(e).sort()).toEqual(['actualDuration', 'estimatedDuration', 'tagList', 'title']);
    expect(validate({ ...filled(), tagList: 'a'.repeat(60) + ',' + 'b'.repeat(60) }, false).tagList).toContain('100 characters in all');
    expect(validate({ ...filled(), estimatedDuration: '2.25', tagList: 'printer, floor-3' }, false)).toEqual({});
  });

  it('sends Markdown and only the fields the user may set', () => {
    const v = { ...filled(), priority: 'High', tagList: ' printer ', owner: 'someone', assignedTo: 'helper', dueDate: '2026-12-24', estimatedDuration: '1.5' };
    expect(toRequest(v, none)).toEqual({
      projectId: 3, title: 'Printer jams', details: 'It **jams**.', isHtml: false, ticketType: 'Problem', category: 'Hardware',
      affectsCustomer: false, dueDate: '2026-12-24', targetDate: null, resolutionDate: null, estimatedDuration: 1.5, actualDuration: null,
    });
    expect(toRequest(v, all)).toMatchObject({ priority: 'High', tagList: 'printer', owner: 'someone', assignedTo: 'helper' });
    expect(toRequest({ ...v, assignedTo: '', priority: '' }, all)).toMatchObject({ priority: null, assignedTo: null });
  });

  it('puts server errors on the fields and keeps the rest as general messages', () => {
    const r = fromServerErrors({ title: ['The Title field is required.'], assignedTo: ['You may not assign tickets.'], '': ['Unable to create'] });
    expect(r.fields).toEqual({ title: 'The Title field is required.', assignedTo: 'You may not assign tickets.' });
    expect(r.general).toEqual(['Unable to create']);
  });

  it('lists errors in form order for the summary', () => {
    expect(summary({ details: 'd', title: 't' }).map((s) => s.field)).toEqual(['title', 'details']);
    expect(summary({ title: 't' })[0]).toEqual({ field: 'title', label: 'Title', message: 't' });
  });
});
