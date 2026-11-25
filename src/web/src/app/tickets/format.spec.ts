import { attachmentUrl, datePart, dueState, fileSize, statusLabel } from './format';

describe('format', () => {
  it('labels statuses like the legacy app', () => {
    expect(statusLabel('MoreInfo')).toBe('More info');
    expect(statusLabel('Active')).toBe('Active');
  });

  it('marks overdue and due-today tickets that are still open', () => {
    expect(dueState('2026-11-09T00:00:00+00:00', 'Active', '2026-11-10')).toBe('overdue');
    expect(dueState('2026-11-10T00:00:00+00:00', 'MoreInfo', '2026-11-10')).toBe('today');
    expect(dueState('2026-11-11T00:00:00+00:00', 'Active', '2026-11-10')).toBeNull();
    expect(dueState('2026-11-01T00:00:00+00:00', 'Closed', '2026-11-10')).toBeNull();
    expect(dueState('2026-11-01T00:00:00+00:00', 'Resolved', '2026-11-10')).toBeNull();
    expect(dueState(null, 'Active', '2026-11-10')).toBeNull();
  });

  it('shows dates as the server stored them', () => {
    expect(datePart('2026-12-24T00:00:00+00:00')).toBe('2026-12-24');
    expect(datePart(null)).toBe('');
  });

  it('formats file sizes', () => {
    expect(fileSize(512)).toBe('512 B');
    expect(fileSize(1536)).toBe('1.5 KB');
    expect(fileSize(20 * 1024 * 1024)).toBe('20 MB');
  });

  it('escapes attachment names in download links', () => {
    expect(attachmentUrl(7, 'scan #1 (copy).pdf')).toBe('/file/7/scan%20%231%20(copy).pdf');
    expect(attachmentUrl(7, '../x')).toBe('/file/7/..%2Fx');
  });
});
