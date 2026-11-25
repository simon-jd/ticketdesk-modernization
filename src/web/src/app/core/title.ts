import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

/** "<page> - TicketDesk", like the legacy pages' titles. */
@Injectable()
export class PageTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    this.set(this.buildTitle(snapshot));
  }

  set(page: string | undefined): void {
    this.title.setTitle(page ? `${page} - TicketDesk` : 'TicketDesk');
  }
}
