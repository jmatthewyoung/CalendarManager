import { Component, ElementRef, OnInit, ViewChild, signal } from '@angular/core';
import {
  CalendarConnectionsClient, CalendarConnectionDto, ColourDto, ConnectionAuditLogDto, SyncClient, SyncLogDto, UsersClient,
  BeginCalendarConnectionCommand, SetCalendarConnectionColorCommand, SetCalendarConnectionVisibilityCommand, UpdateUserSettingsCommand
} from '../web-api-client';
import { PushService } from '../push.service';

function detectTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  } catch {
    return 'UTC';
  }
}

function listTimeZones(): string[] {
  const supportedValuesOf = (Intl as unknown as { supportedValuesOf?: (key: string) => string[] }).supportedValuesOf;
  return supportedValuesOf ? supportedValuesOf('timeZone') : [detectTimeZone()];
}

export const SYNC_STATUS_LABELS: Record<number, string> = {
  0: 'Success',
  1: 'Failed',
  2: 'Needs reauthorization'
};

export const AUDIT_ACTION_LABELS: Record<number, string> = {
  0: 'Connected',
  1: 'Disconnected'
};

export const CALENDAR_PROVIDERS = [
  { id: 0, name: 'Google', className: 'provider-google' },
  { id: 1, name: 'Outlook', className: 'provider-outlook' }
];

export function providerName(provider: number | undefined): string {
  return CALENDAR_PROVIDERS.find(p => p.id === provider)?.name ?? 'Unknown';
}

export function providerRedirectUri(providerId: number): string {
  const provider = CALENDAR_PROVIDERS.find(p => p.id === providerId)!;
  return `${window.location.origin}/connections/callback/${provider.name.toLowerCase()}`;
}

const CONSENT_STORAGE_KEY = 'calendarManagerOAuthConsent';

@Component({
  standalone: false,
  selector: 'app-connections',
  templateUrl: './connections.component.html',
  styleUrls: ['./connections.component.scss']
})
export class ConnectionsComponent implements OnInit {
  @ViewChild('disconnectDialog') disconnectDialogRef: ElementRef<HTMLDialogElement>;
  @ViewChild('consentDialog') consentDialogRef: ElementRef<HTMLDialogElement>;

  providers = CALENDAR_PROVIDERS;

  connections = signal<CalendarConnectionDto[] | null>(null);
  colours = signal<ColourDto[]>([]);
  connecting = signal<number | null>(null);
  resyncingId = signal<number | null>(null);
  colourEditorId = signal<number | null>(null);
  disconnectTarget = signal<CalendarConnectionDto | null>(null);
  error = signal('');

  private pendingConsentProviderId: number | null = null;
  consentChecked = signal(false);

  syncLogs = signal<SyncLogDto[] | null>(null);
  showActivity = signal(false);

  auditLog = signal<ConnectionAuditLogDto[] | null>(null);
  showAuditLog = signal(false);

  timeZones = listTimeZones();
  selectedTimeZone = signal(detectTimeZone());
  timeZoneSaved = signal(false);
  savingTimeZone = signal(false);

  constructor(
    private connectionsClient: CalendarConnectionsClient,
    private syncClient: SyncClient,
    private usersClient: UsersClient,
    public pushService: PushService
  ) {}

  ngOnInit(): void {
    this.load();

    this.usersClient.getUserSettings().subscribe({
      next: settings => this.selectedTimeZone.set(settings.timeZoneId || detectTimeZone()),
      error: error => console.error(error)
    });
  }

  saveTimeZone(): void {
    this.savingTimeZone.set(true);
    this.timeZoneSaved.set(false);

    this.usersClient.updateUserSettings(new UpdateUserSettingsCommand({ timeZoneId: this.selectedTimeZone() })).subscribe({
      next: () => {
        this.savingTimeZone.set(false);
        this.timeZoneSaved.set(true);
      },
      error: error => {
        console.error(error);
        this.savingTimeZone.set(false);
      }
    });
  }

  private load(): void {
    this.connectionsClient.getCalendarConnections().subscribe({
      next: vm => {
        this.connections.set(vm.connections ?? []);
        this.colours.set(vm.colours ?? []);
      },
      error: error => console.error(error)
    });
  }

  toggleActivity(): void {
    this.showActivity.update(v => !v);

    if (this.showActivity() && !this.syncLogs()) {
      this.syncClient.getSyncLogs().subscribe({
        next: logs => this.syncLogs.set(logs),
        error: error => console.error(error)
      });
    }
  }

  connectionLabel(calendarConnectionId: number | undefined): string {
    return this.connections()?.find(c => c.id === calendarConnectionId)?.accountEmail ?? 'Unknown calendar';
  }

  statusLabel(status: number | undefined): string {
    return SYNC_STATUS_LABELS[status ?? 0] ?? 'Unknown';
  }

  toggleAuditLog(): void {
    this.showAuditLog.update(v => !v);

    if (this.showAuditLog() && !this.auditLog()) {
      this.connectionsClient.getConnectionAuditLog().subscribe({
        next: log => this.auditLog.set(log),
        error: error => console.error(error)
      });
    }
  }

  auditActionLabel(action: number | undefined): string {
    return AUDIT_ACTION_LABELS[action ?? 0] ?? 'Unknown';
  }

  toggleNotifications(): void {
    if (this.pushService.subscribed()) {
      this.pushService.disable();
    } else {
      this.pushService.enable();
    }
  }

  providerName(provider: number | undefined): string {
    return providerName(provider);
  }

  providerClass(provider: number | undefined): string {
    return CALENDAR_PROVIDERS.find(p => p.id === provider)?.className ?? '';
  }

  connect(providerId: number): void {
    if (localStorage.getItem(CONSENT_STORAGE_KEY) === 'true') {
      this.beginConnect(providerId);
      return;
    }

    this.pendingConsentProviderId = providerId;
    this.consentChecked.set(false);
    this.consentDialogRef.nativeElement.showModal();
  }

  cancelConsent(): void {
    this.consentDialogRef.nativeElement.close();
    this.pendingConsentProviderId = null;
  }

  confirmConsent(): void {
    if (!this.consentChecked()) return;

    localStorage.setItem(CONSENT_STORAGE_KEY, 'true');
    this.consentDialogRef.nativeElement.close();

    const providerId = this.pendingConsentProviderId;
    this.pendingConsentProviderId = null;
    if (providerId !== null) {
      this.beginConnect(providerId);
    }
  }

  private beginConnect(providerId: number): void {
    this.connecting.set(providerId);
    this.error.set('');

    const command = new BeginCalendarConnectionCommand({
      provider: providerId,
      redirectUri: providerRedirectUri(providerId)
    });

    this.connectionsClient.beginCalendarConnection(command).subscribe({
      next: url => window.location.href = url,
      error: error => {
        console.error(error);
        this.connecting.set(null);
        this.error.set('Could not start the connection. Please try again.');
      }
    });
  }

  toggleVisibility(connection: CalendarConnectionDto): void {
    const isVisible = !connection.isVisible;
    this.connectionsClient
      .setCalendarConnectionVisibility(connection.id!, new SetCalendarConnectionVisibilityCommand({ id: connection.id!, isVisible }))
      .subscribe({
        next: () => this.connections.update(cs => cs!.map(c => c.id === connection.id ? { ...c, isVisible } as CalendarConnectionDto : c)),
        error: error => console.error(error)
      });
  }

  showColourEditor(connection: CalendarConnectionDto): void {
    this.colourEditorId.set(this.colourEditorId() === connection.id ? null : connection.id!);
  }

  setColour(connection: CalendarConnectionDto, colour: string): void {
    this.connectionsClient
      .setCalendarConnectionColor(connection.id!, new SetCalendarConnectionColorCommand({ id: connection.id!, colour }))
      .subscribe({
        next: () => {
          this.connections.update(cs => cs!.map(c => c.id === connection.id ? { ...c, colour } as CalendarConnectionDto : c));
          this.colourEditorId.set(null);
        },
        error: error => console.error(error)
      });
  }

  resync(connection: CalendarConnectionDto): void {
    this.resyncingId.set(connection.id!);
    this.connectionsClient.resyncCalendarConnection(connection.id!).subscribe({
      next: () => {
        this.resyncingId.set(null);
        this.connections.update(cs => cs!.map(c => c.id === connection.id
          ? { ...c, lastSyncedAtUtc: new Date(), needsReauth: false } as CalendarConnectionDto
          : c));

        if (this.showActivity()) {
          this.syncClient.getSyncLogs().subscribe({
            next: logs => this.syncLogs.set(logs),
            error: error => console.error(error)
          });
        }
      },
      error: error => {
        console.error(error);
        this.resyncingId.set(null);
      }
    });
  }

  confirmDisconnect(connection: CalendarConnectionDto): void {
    this.disconnectTarget.set(connection);
    this.disconnectDialogRef.nativeElement.showModal();
  }

  cancelDisconnect(): void {
    this.disconnectDialogRef.nativeElement.close();
    this.disconnectTarget.set(null);
  }

  disconnect(): void {
    const target = this.disconnectTarget()!;
    this.connectionsClient.disconnectCalendarConnection(target.id!).subscribe({
      next: () => {
        this.connections.update(cs => cs!.filter(c => c.id !== target.id));
        this.disconnectDialogRef.nativeElement.close();
        this.disconnectTarget.set(null);

        if (this.showAuditLog()) {
          this.connectionsClient.getConnectionAuditLog().subscribe({
            next: log => this.auditLog.set(log),
            error: error => console.error(error)
          });
        }
      },
      error: error => console.error(error)
    });
  }
}
