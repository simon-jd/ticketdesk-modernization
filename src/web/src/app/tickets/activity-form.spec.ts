import { activityRequest, fieldsFor, validateActivity } from './activity-form';

const empty = { comment: '', assignedTo: '', priority: '', assignToMe: false, reactivate: false };

describe('activity forms', () => {
  it('ask for the fields of each activity, like the legacy partials', () => {
    expect(fieldsFor('Assign')).toEqual({ commentRequired: false, assignee: true, priority: true, assignToMe: false, reactivate: false });
    expect(fieldsFor('TakeOver')).toMatchObject({ assignee: false, priority: true });
    expect(fieldsFor('ReOpen')).toMatchObject({ commentRequired: true, assignToMe: true });
    expect(fieldsFor('SupplyMoreInfo')).toMatchObject({ commentRequired: true, reactivate: true });
    expect(fieldsFor('Close')).toMatchObject({ commentRequired: false, assignee: false, priority: false });
  });

  it('require a comment where the legacy form did and an assignee for assignments', () => {
    expect(validateActivity('Resolve', empty)).toEqual({ comment: 'Enter a comment.' });
    expect(validateActivity('Pass', empty)).toEqual({ assignedTo: 'Choose who gets the ticket.' });
    expect(validateActivity('Close', empty)).toEqual({});
  });

  it('send only the fields of the activity', () => {
    const v = { comment: ' done ', assignedTo: 'h1', priority: 'High', assignToMe: true, reactivate: true };
    expect(activityRequest('Resolve', v)).toEqual({ activity: 'Resolve', comment: ' done ' });
    expect(activityRequest('Assign', v)).toEqual({ activity: 'Assign', comment: ' done ', assignedTo: 'h1', priority: 'High' });
    expect(activityRequest('ReOpen', { ...v, comment: '' })).toEqual({ activity: 'ReOpen', comment: null, assignToMe: true });
    expect(activityRequest('SupplyMoreInfo', v)).toMatchObject({ reactivate: true });
  });
});
