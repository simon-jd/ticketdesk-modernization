import { ariaSort, filtersFor, fromRoute, pageWindow, parseSort, rangeText, toApiParams, toUrl, toggleSort } from './list-query';

const lists = ['mytickets', 'opentickets', 'historytickets'];

describe('list query', () => {
  it('reads the URL and falls back to defaults for anything unknown', () => {
    const q = fromRoute({ listName: 'OpenTickets', page: '3', sort: '-lastUpdateDate,title', status: 'MoreInfo', pageSize: '50', owner: 'u1' }, lists, 'mytickets', null);
    expect(q).toEqual({ list: 'opentickets', page: 3, pageSize: 50, sort: '-lastUpdateDate,title', status: 'MoreInfo', owner: 'u1', assignedTo: '', projectId: null });

    const d = fromRoute({ listName: 'unassigned', page: '0', sort: 'details,-title', status: 'Open', pageSize: '7', projectId: 'x' }, lists, 'mytickets', 2);
    expect(d).toEqual({ list: 'mytickets', page: 1, pageSize: 20, sort: '-title', status: '', owner: '', assignedTo: '', projectId: 2 });
  });

  it('uses the project chosen in the project menu unless the URL says otherwise', () => {
    expect(fromRoute({}, lists, 'mytickets', 4).projectId).toBe(4);
    expect(fromRoute({ projectId: 'all' }, lists, 'mytickets', 4).projectId).toBeNull();
    expect(fromRoute({ projectId: '9' }, lists, 'mytickets', 4).projectId).toBe(9);
  });

  it('writes only what differs from the defaults into the URL', () => {
    const q = fromRoute({ listName: 'opentickets' }, lists, 'mytickets', 4);
    expect(toUrl(q, 4)).toEqual({ commands: ['/tickets', 'opentickets'], queryParams: {} });
    expect(toUrl({ ...q, page: 2, sort: 'title', pageSize: 50, projectId: null, status: 'Active' }, 4)).toEqual({
      commands: ['/tickets', 'opentickets', 2],
      queryParams: { sort: 'title', pageSize: '50', projectId: 'all', status: 'Active' },
    });
  });

  it('round-trips through the URL', () => {
    const q = { list: 'historytickets', page: 4, pageSize: 10, sort: 'priority', status: 'Closed' as const, owner: 'a', assignedTo: 'b', projectId: 3 };
    const { commands, queryParams } = toUrl(q, null);
    const back = fromRoute({ listName: String(commands[1]), page: String(commands[2]), ...queryParams }, lists, 'mytickets', null);
    expect(back).toEqual(q);
  });

  it('maps to the API parameters', () => {
    expect(toApiParams({ list: 'mytickets', page: 2, pageSize: 20, sort: '', status: 'Active', owner: '', assignedTo: 'h1', projectId: null })).toEqual({
      List: 'mytickets', Page: 2, PageSize: 20, Status: ['Active'], AssignedTo: 'h1',
    });
  });

  it('toggles the sort direction of the first column and starts new columns ascending (dates newest first)', () => {
    expect(toggleSort('-lastUpdateDate,ticketId', 'lastUpdateDate')).toBe('lastUpdateDate');
    expect(toggleSort('title,ticketId', 'title')).toBe('-title');
    expect(toggleSort('-lastUpdateDate,ticketId', 'title')).toBe('title');
    expect(toggleSort('title,ticketId', 'dueDate')).toBe('-dueDate');
    expect(parseSort(' -a, b ,,')).toEqual([{ field: 'a', descending: true }, { field: 'b', descending: false }]);
  });

  it('tells screen readers how a column is sorted', () => {
    expect(ariaSort('-lastUpdateDate,ticketId', 'lastUpdateDate')).toBe('descending');
    expect(ariaSort('title,ticketId', 'title')).toBe('ascending');
    expect(ariaSort('title,ticketId', 'ticketId')).toBe('none');
  });

  it('shows a window of page numbers around the current page', () => {
    expect(pageWindow(1, 0)).toEqual([]);
    expect(pageWindow(1, 3)).toEqual([1, 2, 3]);
    expect(pageWindow(1, 10)).toEqual([1, 2, 3, 4, 5]);
    expect(pageWindow(6, 10)).toEqual([4, 5, 6, 7, 8]);
    expect(pageWindow(10, 10)).toEqual([6, 7, 8, 9, 10]);
  });

  it('describes the range shown', () => {
    expect(rangeText(1, 20, 0)).toBe('No tickets');
    expect(rangeText(2, 20, 57)).toBe('Tickets 21–40 of 57');
    expect(rangeText(3, 20, 57)).toBe('Tickets 41–57 of 57');
    expect(rangeText(9, 20, 57)).toBe('No tickets on page 9 (57 in all)');
  });

  it('hides the filters a list already fixes, like the legacy filter bar', () => {
    expect(filtersFor('mytickets')).toEqual({ status: true, owner: false, assignedTo: true });
    expect(filtersFor('unassigned')).toEqual({ status: true, owner: true, assignedTo: false });
    expect(filtersFor('historytickets')).toEqual({ status: false, owner: true, assignedTo: true });
  });
});
