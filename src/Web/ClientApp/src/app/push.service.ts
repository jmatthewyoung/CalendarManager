import { Injectable, signal } from '@angular/core';
import { SwPush } from '@angular/service-worker';
import { firstValueFrom } from 'rxjs';
import { PushClient, RegisterPushSubscriptionCommand } from './web-api-client';

@Injectable({ providedIn: 'root' })
export class PushService {
  readonly supported = this.swPush.isEnabled;

  subscribed = signal(false);
  busy = signal(false);
  error = signal('');

  constructor(private swPush: SwPush, private pushClient: PushClient) {
    if (this.supported) {
      this.swPush.subscription.subscribe(subscription => this.subscribed.set(!!subscription));
    }
  }

  async enable(): Promise<void> {
    if (!this.supported) return;

    this.busy.set(true);
    this.error.set('');

    try {
      const publicKey = await firstValueFrom(this.pushClient.getPushPublicKey());
      const subscription = await this.swPush.requestSubscription({ serverPublicKey: publicKey });
      const json = subscription.toJSON();

      await firstValueFrom(this.pushClient.subscribe(new RegisterPushSubscriptionCommand({
        endpoint: json.endpoint!,
        p256dhKey: json.keys!['p256dh'],
        authKey: json.keys!['auth']
      })));

      this.subscribed.set(true);
    } catch (error) {
      console.error(error);
      this.error.set('Could not enable notifications. Check your browser permission settings.');
    } finally {
      this.busy.set(false);
    }
  }

  async disable(): Promise<void> {
    if (!this.supported) return;

    this.busy.set(true);
    this.error.set('');

    try {
      const subscription = await firstValueFrom(this.swPush.subscription);
      const endpoint = subscription?.endpoint;

      await this.swPush.unsubscribe();

      if (endpoint) {
        await firstValueFrom(this.pushClient.unsubscribe(endpoint));
      }

      this.subscribed.set(false);
    } catch (error) {
      console.error(error);
      this.error.set('Could not disable notifications.');
    } finally {
      this.busy.set(false);
    }
  }
}
