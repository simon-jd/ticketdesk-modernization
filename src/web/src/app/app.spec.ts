import { HttpXsrfTokenExtractor, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { SessionInfo } from './api/models/session-info';
import { Session } from './core/session';

const session: SessionInfo = {
  userId: 'u-admin', displayName: 'Ada Admin', isHelpDesk: true, isAdministrator: true,
  lists: [{ name: 'unassigned', displayName: 'Unassigned' }], defaultList: 'unassigned', selectedProjectId: null, multiProject: false,
  today: '2026-11-13', angularPages: ['tickets.index', 'ticket.view', 'ticket.new.get'],
};

describe('page frame', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('signs out with a form that posts the antiforgery token, not with a link (S3)', () => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: HttpXsrfTokenExtractor, useValue: { getToken: () => 'token-from-the-shell' } },
      ],
    });
    TestBed.inject(Session).info.set(structuredClone(session));
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    const form = root.querySelector<HTMLFormElement>('form.sign-out')!;
    expect(form).not.toBeNull();
    expect(form.getAttribute('method')).toBe('post');
    expect(form.getAttribute('action')).toBe('/user/sign-out');
    expect(form.querySelector<HTMLInputElement>('input[type="hidden"][name="__RequestVerificationToken"]')!.value).toBe('token-from-the-shell');
    expect(form.querySelector('button[type="submit"]')!.textContent!.trim()).toBe('Log off');
    expect(root.querySelector('a[href="/user/sign-out"]')).toBeNull();
  });

  it('shows no sign-out form before the session is loaded', () => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('form.sign-out')).toBeNull();
  });
});
