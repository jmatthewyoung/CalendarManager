import { Component, ElementRef, OnInit, ViewChild, signal } from '@angular/core';
import {
  CalendarConnectionsClient, CalendarConnectionDto, ColourDto,
  BeginCalendarConnectionCommand, SetCalendarConnectionColorCommand, SetCalendarConnectionVisibilityCommand
} from '../web-api-client';

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

@Component({
  standalone: false,
  selector: 'app-connections',
  templateUrl: './connections.component.html',
  styleUrls: ['./connections.component.scss']
})
export class ConnectionsComponent implements OnInit {
  @ViewChild('disconnectDialog') disconnectDialogRef: ElementRef<HTMLDialogElement>;

  providers = CALENDAR_PROVIDERS;

  connections = signal<CalendarConnectionDto[] | null>(null);
  colours = signal<ColourDto[]>([]);
  connecting = signal<number | null>(null);
  resyncingId = signal<number | null>(null);
  colourEditorId = signal<number | null>(null);
  disconnectTarget = signal<CalendarConnectionDto | null>(null);
  error = signal('');

  constructor(private connectionsClient: CalendarConnectionsClient) {}

  ngOnInit(): void {
    this.load();
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

  providerName(provider: number | undefined): string {
    return providerName(provider);
  }

  providerClass(provider: number | undefined): string {
    return CALENDAR_PROVIDERS.find(p => p.id === provider)?.className ?? '';
  }

  connect(providerId: number): void {
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
      },
      error: error => console.error(error)
    });
  }
}
