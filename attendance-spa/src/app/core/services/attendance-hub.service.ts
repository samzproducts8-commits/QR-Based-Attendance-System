import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

import { LiveDashboardMetrics } from '../models/attendance.models';

export interface KioskQrUpdate {
  base64Png: string;
  tokenValue: string;
  expiresAt: string;
}

export type HubConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'error';

@Injectable({ providedIn: 'root' })
export class AttendanceHubService {
  private connection: signalR.HubConnection | null = null;

  private readonly qrUpdateSubject = new BehaviorSubject<KioskQrUpdate | null>(null);
  readonly qrUpdate$: Observable<KioskQrUpdate | null> = this.qrUpdateSubject.asObservable();

  private readonly liveDashboardSubject = new BehaviorSubject<LiveDashboardMetrics | null>(null);
  readonly liveDashboardUpdate$: Observable<LiveDashboardMetrics | null> = this.liveDashboardSubject.asObservable();

  private readonly statusSubject = new BehaviorSubject<HubConnectionStatus>('disconnected');
  readonly status$: Observable<HubConnectionStatus> = this.statusSubject.asObservable();

  private readonly errorSubject = new BehaviorSubject<string | null>(null);
  readonly error$: Observable<string | null> = this.errorSubject.asObservable();

  async connect(): Promise<void> {
    if (this.connection) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect()
      .build();

    this.connection.on('ReceiveQrCode', (base64Png: string, tokenValue: string, expiresAt: string) => {
      this.qrUpdateSubject.next({ base64Png, tokenValue, expiresAt });
    });

    this.connection.on('ReceiveLiveDashboardUpdate', (metrics: LiveDashboardMetrics) => {
      this.liveDashboardSubject.next(metrics);
    });

    this.connection.onreconnecting(() => this.statusSubject.next('reconnecting'));
    this.connection.onreconnected(() => {
      this.statusSubject.next('connected');
      this.requestCurrentQr();
    });
    this.connection.onclose(() => this.statusSubject.next('disconnected'));

    this.errorSubject.next(null);
    this.statusSubject.next('connecting');

    try {
      await this.connection.start();
      this.statusSubject.next('connected');
      await this.requestCurrentQr();
    } catch {
      this.connection = null;
      this.statusSubject.next('error');
      this.errorSubject.next('Could not connect to the server. Retrying…');
    }
  }

  async requestCurrentQr(): Promise<void> {
    await this.connection?.invoke('RequestCurrentQr');
  }

  async disconnect(): Promise<void> {
    await this.connection?.stop();
    this.connection = null;
    this.statusSubject.next('disconnected');
  }
}
