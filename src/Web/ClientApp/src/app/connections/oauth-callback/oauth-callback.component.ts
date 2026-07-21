import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CalendarConnectionsClient, CompleteCalendarConnectionCommand } from '../../web-api-client';
import { CALENDAR_PROVIDERS, providerRedirectUri } from '../connections.component';

@Component({
  standalone: false,
  selector: 'app-oauth-callback',
  templateUrl: './oauth-callback.component.html',
  styleUrls: ['./oauth-callback.component.scss']
})
export class OAuthCallbackComponent implements OnInit {
  error = signal('');

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private connectionsClient: CalendarConnectionsClient
  ) {}

  ngOnInit(): void {
    const providerParam = this.route.snapshot.paramMap.get('provider');
    const provider = CALENDAR_PROVIDERS.find(p => p.name.toLowerCase() === providerParam);
    const params = this.route.snapshot.queryParamMap;
    const oauthError = params.get('error');
    const code = params.get('code');
    const state = params.get('state');

    if (!provider || oauthError || !code || !state) {
      this.error.set(oauthError ? `The provider declined the request: ${oauthError}` : 'The connection request was missing required information.');
      return;
    }

    const command = new CompleteCalendarConnectionCommand({
      provider: provider.id,
      code,
      state,
      redirectUri: providerRedirectUri(provider.id)
    });

    this.connectionsClient.completeCalendarConnection(command).subscribe({
      next: () => this.router.navigate(['/connections']),
      error: error => {
        console.error(error);
        this.error.set('Could not complete the connection. Please try again.');
      }
    });
  }
}
