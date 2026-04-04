import { CommonModule, DOCUMENT } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom, forkJoin } from 'rxjs';
import {
  AdminUserSummary,
  CogCheckStatus,
  CogRuntimeSettings,
  CogSession,
  CraftGearPayload,
  CreateMarketplaceListingPayload,
  DashboardResponse,
  GrantCogsPayload,
  GrantGearPayload,
  InventoryItem,
  MarketplaceListing,
  StoreItem,
  UpdateCogWarningIntervalPayload,
  UpsertGearPayload,
  UserProfile
} from './models/economy.models';
import { EconomyApiService } from './services/economy-api.service';

type CogPage = 'pilot' | 'shop' | 'locker' | 'marketplace' | 'timecard' | 'admin';
type LockerSort = 'quantityDesc' | 'nameAsc' | 'typeAsc' | 'valueDesc';
const DISPLAY_NAME_MAX_LENGTH = 120;

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly api = inject(EconomyApiService);
  private readonly documentRef = inject(DOCUMENT);
  readonly displayNameMaxLength = DISPLAY_NAME_MAX_LENGTH;

  isLoading = true;
  hasCompletedInitialLoad = false;
  isAuthenticated = false;
  requiresDisplayName = false;
  displayNameDraft = '';
  errorMessage = '';
  infoMessage = '';
  activePage: CogPage = 'pilot';

  dashboard: DashboardResponse | null = null;
  marketplaceListings: MarketplaceListing[] = [];
  cogSessionHistory: CogSession[] = [];
  cogCheckStatus: CogCheckStatus | null = null;
  cogRuntimeSettings: CogRuntimeSettings | null = null;
  adminUsers: AdminUserSummary[] = [];
  adminGearItems: StoreItem[] = [];
  warningIntervalForm: UpdateCogWarningIntervalPayload = {
    warningIntervalMinutes: 60
  };

  grantCogsForm: GrantCogsPayload = {
    userAccountId: 0,
    amount: 25,
    note: 'Standard disbursement per Central Authority directive.'
  };

  grantGearForm: GrantGearPayload = {
    userAccountId: 0,
    gearItemId: 0,
    quantity: 1,
    note: 'Authorized gear allocation - see case file.'
  };

  gearForm: UpsertGearPayload = this.createEmptyGearForm();
  editingGearItemId: number | null = null;

  craftForm: CraftGearPayload = this.createEmptyCraftForm();

  listingForm: CreateMarketplaceListingPayload = {
    gearItemId: 0,
    quantity: 1,
    priceInCogs: 40,
    sellerNote: 'Fresh from my cog forge.'
  };

  cogInNote = '';
  cogOutNote = '';
  lockerSearch = '';
  lockerFilterType = 'All';
  lockerSort: LockerSort = 'quantityDesc';
  showCogCheckOverlay = false;
  isCogHandleDragging = false;
  cogCheckSpinCount = 0;
  cogCheckHandleRotation = 0;
  private cogCheckProgressDegrees = 0;
  private cogCheckDirection: 1 | -1 | 0 = 0;
  private cogHandleActivePointerId: number | null = null;
  private cogHandleLastPointerAngle: number | null = null;
  private cogCheckPollHandle: ReturnType<typeof setInterval> | null = null;
  private lastNotifiedAutoCogOutSessionId: number | null = null;
  private cogCheckAlarmAudio: HTMLAudioElement | null = null;
  private isCogCheckAlarmBlockedByAutoplay = false;
  private hasPlayedCogCheckAlarmForCurrentOverlay = false;

  async ngOnInit(): Promise<void> {
    this.initializeCogCheckAlarm();
    await this.refreshAll();
  }

  ngOnDestroy(): void {
    this.stopCogCheckPolling();
    this.resetCogCheckHandle();
    this.stopCogCheckAlarm();
    this.setCogCheckPageLock(false);
  }

  get pilot(): UserProfile | null {
    return this.dashboard?.pilot ?? null;
  }

  get pilotDisplayNameLabel(): string {
    const displayName = this.pilot?.displayName ?? '';
    const trimmed = displayName.trim();
    return trimmed || 'Uncalibrated Citizen';
  }

  get showStartupLoadingOverlay(): boolean {
    return this.isLoading && !this.hasCompletedInitialLoad;
  }

  get storeFront(): StoreItem[] {
    return this.dashboard?.storeFront ?? [];
  }

  get hasAdminPanel(): boolean {
    return this.pilot?.isAdmin ?? false;
  }

  get openCogSession(): CogSession | null {
    return this.cogSessionHistory.find(x => x.isOpen) ?? null;
  }

  get hasOpenCogSession(): boolean {
    return this.cogCheckStatus?.hasOpenSession ?? this.openCogSession !== null;
  }

  get cogCheckSpinsRequired(): number {
    return this.cogCheckStatus?.spinsRequired ?? 15;
  }

  get cogCheckSpinsRemaining(): number {
    return Math.max(0, this.cogCheckSpinsRequired - this.cogCheckSpinCount);
  }

  get cogCheckCountdownSeconds(): number {
    const deadline = this.toUtcMilliseconds(this.cogCheckStatus?.cogCheckDeadlineAtUtc);
    if (deadline === null) {
      return 0;
    }

    return Math.max(0, Math.ceil((deadline - Date.now()) / 1000));
  }

  get selectedListingItem(): UserProfile['inventory'][number] | null {
    const inventory = this.pilot?.inventory ?? [];
    return inventory.find(x => x.gearItemId === this.listingForm.gearItemId) ?? null;
  }

  get lockerTypeOptions(): string[] {
    const types = new Set((this.pilot?.inventory ?? []).map(x => x.gearType).filter(Boolean));
    return ['All', ...Array.from(types).sort((a, b) => a.localeCompare(b))];
  }

  get filteredLockerInventory(): InventoryItem[] {
    const inventory = this.pilot?.inventory ?? [];
    const search = this.lockerSearch.trim().toLowerCase();
    const selectedType = this.lockerFilterType;

    const filtered = inventory.filter(item =>
    {
      const typeMatch = selectedType === 'All' || item.gearType === selectedType;
      if (!typeMatch)
      {
        return false;
      }

      if (!search)
      {
        return true;
      }

      const nameMatch = item.name.toLowerCase().includes(search);
      const typeSearchMatch = item.gearType.toLowerCase().includes(search);
      const flavorMatch = (item.flavorText ?? '').toLowerCase().includes(search);
      return nameMatch || typeSearchMatch || flavorMatch;
    });

    const sorted = [...filtered];
    sorted.sort((a, b) =>
    {
      switch (this.lockerSort)
      {
        case 'nameAsc':
          return a.name.localeCompare(b.name);
        case 'typeAsc':
          return a.gearType.localeCompare(b.gearType) || a.name.localeCompare(b.name);
        case 'valueDesc':
          return (b.costInCogs * b.quantity) - (a.costInCogs * a.quantity);
        case 'quantityDesc':
        default:
          return b.quantity - a.quantity || a.name.localeCompare(b.name);
      }
    });

    return sorted;
  }

  get liveCogBalance(): number {
    return this.pilot?.cogBalance ?? 0;
  }

  get adminHeadcount(): number {
    return this.adminUsers.length;
  }

  get cogCirculation(): number {
    return this.adminUsers.reduce((total, user) => total + user.cogBalance, 0);
  }

  get selectedGrantCogsTarget(): AdminUserSummary | null {
    return this.adminUsers.find(x => x.userAccountId === this.grantCogsForm.userAccountId) ?? null;
  }

  get selectedGrantGearTarget(): AdminUserSummary | null {
    return this.adminUsers.find(x => x.userAccountId === this.grantGearForm.userAccountId) ?? null;
  }

  setPage(page: CogPage): void {
    if (page === 'admin' && !this.hasAdminPanel) {
      return;
    }

    this.activePage = page;
  }

  isPage(page: CogPage): boolean {
    return this.activePage === page;
  }

  signIn(): void {
    this.errorMessage = '';
    this.infoMessage = 'Redirecting to identity verification...';
    this.api.startGoogleLogin();
  }

  async signOut(): Promise<void> {
    this.errorMessage = '';

    try {
      await firstValueFrom(this.api.logout());
    } catch {
      // Sign-out still proceeds client-side so the user can re-enter auth flow.
    }

    this.applyUnauthenticatedState('Session terminated. Your cogs remain on file with the Authority.');
  }

  async refreshAll(): Promise<void> {
    this.isLoading = true;
    this.errorMessage = '';

    try {
      let profile: UserProfile;
      try {
        profile = await firstValueFrom(this.api.getCurrentUserProfile());
      } catch (error) {
        if (error instanceof HttpErrorResponse) {
          if (error.status === 401) {
            this.applyUnauthenticatedState('No active session. The economy continues without you.');
            return;
          }

          if (error.status === 404) {
            this.applyUnauthenticatedState('Cog Slop API route mesh unavailable. Check deployment alignment.');
            return;
          }
        }

        throw error;
      }

      if (!this.hasConfiguredDisplayName(profile.displayName)) {
        this.beginDisplayNameOnboarding(profile);
        return;
      }

      let dashboard: DashboardResponse;
      try {
        dashboard = await firstValueFrom(this.api.getDashboard());
      } catch (error) {
        if (error instanceof HttpErrorResponse) {
          if (error.status === 401) {
            this.applyUnauthenticatedState('No active session. The economy continues without you.');
            return;
          }

          if (error.status === 428) {
            this.beginDisplayNameOnboarding(profile);
            return;
          }

          if (error.status === 404) {
            this.applyUnauthenticatedState('Cog Slop API route mesh unavailable. Check deployment alignment.');
            return;
          }
        }

        throw error;
      }

      this.isAuthenticated = true;
      this.requiresDisplayName = false;
      this.displayNameDraft = dashboard.pilot.displayName;
      this.dashboard = dashboard;
      await this.loadMarketplaceAndSessions();
      await this.refreshCogCheckStatus();
      this.ensureListingFormTargets();
      this.startCogCheckPolling();

      if (dashboard.pilot.isAdmin) {
        await this.loadAdminPanel();
      } else {
        this.adminUsers = [];
        this.adminGearItems = [];
        this.cogRuntimeSettings = null;

        if (this.activePage === 'admin') {
          this.activePage = 'pilot';
        }
      }
    } catch (error) {
      this.captureError(error, 'The cog reserve encountered a structural integrity failure.');
    } finally {
      this.isLoading = false;
      this.hasCompletedInitialLoad = true;
    }
  }

  async saveDisplayName(): Promise<void> {
    const displayName = this.displayNameDraft.trim();
    if (!displayName) {
      this.errorMessage = 'Display name is required before entering Cog Slop.';
      return;
    }

    if (displayName.length > this.displayNameMaxLength) {
      this.errorMessage = `Display name must be ${this.displayNameMaxLength} characters or fewer.`;
      return;
    }

    this.errorMessage = '';

    try {
      const profile = await firstValueFrom(this.api.updateDisplayName({ displayName }));
      this.displayNameDraft = profile.displayName;
      this.infoMessage = this.requiresDisplayName
        ? 'Display name calibrated. Entering Cog Slop.'
        : 'Display name updated.';
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Display name update failed. Calibration denied.');
    }
  }

  async buyGear(item: StoreItem): Promise<void> {
    this.errorMessage = '';

    try {
      const receipt = await firstValueFrom(this.api.buyGear(item.gearItemId, 1));
      this.infoMessage = receipt.message;
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Transaction rejected by the cog clearinghouse.');
    }
  }

  async craftGear(): Promise<void> {
    this.errorMessage = '';

    const payload: CraftGearPayload = {
      ...this.craftForm,
      name: this.craftForm.name.trim(),
      gearType: this.craftForm.gearType.trim(),
      description: this.normalizeText(this.craftForm.description),
      flavorText: this.normalizeText(this.craftForm.flavorText)
    };

    if (!payload.name || !payload.gearType || payload.craftingCostInCogs < 1) {
      this.errorMessage = 'Crafting requires a name, gear type, and positive cog cost.';
      return;
    }

    try {
      const receipt = await firstValueFrom(this.api.craftGear(payload));
      this.infoMessage = receipt.message;
      this.craftForm = this.createEmptyCraftForm();
      await this.refreshAll();
      this.setPage('locker');
    } catch (error) {
      this.captureError(error, 'Crafting failed. The forge rejected your schematic.');
    }
  }

  async createMarketplaceListing(): Promise<void> {
    this.errorMessage = '';
    const selectedItem = this.selectedListingItem;

    if (!selectedItem) {
      this.errorMessage = 'Choose a gear item from your locker before listing.';
      return;
    }

    const quantity = Math.floor(this.listingForm.quantity);
    const priceInCogs = Math.floor(this.listingForm.priceInCogs);

    if (quantity < 1 || quantity > selectedItem.quantity) {
      this.errorMessage = 'Listing quantity must be at least 1 and within your locker holdings.';
      return;
    }

    if (priceInCogs < 1) {
      this.errorMessage = 'Listing price must be a positive cog amount.';
      return;
    }

    try {
      await firstValueFrom(this.api.createMarketplaceListing({
        gearItemId: selectedItem.gearItemId,
        quantity,
        priceInCogs,
        sellerNote: this.normalizeText(this.listingForm.sellerNote)
      }));

      this.infoMessage = 'Listing posted. The cog bazaar is now watching.';
      await this.refreshAll();
      this.setPage('marketplace');
    } catch (error) {
      this.captureError(error, 'Listing creation failed. Marketplace registry denied the submission.');
    }
  }

  async buyMarketplaceListing(listing: MarketplaceListing): Promise<void> {
    this.errorMessage = '';

    try {
      const receipt = await firstValueFrom(this.api.buyMarketplaceListing(listing.marketplaceListingId));
      this.infoMessage = receipt.message;
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Marketplace purchase denied by the exchange engine.');
    }
  }

  async cogIn(): Promise<void> {
    this.errorMessage = '';

    try {
      await firstValueFrom(this.api.cogIn(this.normalizeText(this.cogInNote)));
      this.infoMessage = 'You are now cogged in. Shift timer engaged.';
      this.cogInNote = '';
      await this.refreshAll();
      this.setPage('timecard');
    } catch (error) {
      this.captureError(error, 'Cog-in failed. Shift authorization denied.');
    }
  }

  async cogOut(): Promise<void> {
    this.errorMessage = '';

    try {
      const session = await firstValueFrom(this.api.cogOut(this.normalizeText(this.cogOutNote)));
      const payout = session.payoutCogs ?? 0;
      this.infoMessage = `Cog-out recorded. Shift duration: ${this.formatDurationMinutes(session.durationMinutes)}. Payout: ${payout} cogs.`;
      this.cogOutNote = '';
      await this.refreshAll();
      this.setPage('timecard');
    } catch (error) {
      this.captureError(error, 'Cog-out failed. No active shift was found.');
    }
  }

  listingOwnershipLabel(listing: MarketplaceListing): string {
    if (listing.sellerUserAccountId === this.pilot?.userAccountId) {
      return 'Your listing';
    }

    return `Seller: ${listing.sellerDisplayName}`;
  }

  isOwnListing(listing: MarketplaceListing): boolean {
    return listing.sellerUserAccountId === this.pilot?.userAccountId;
  }

  canAffordListing(listing: MarketplaceListing): boolean {
    return (this.pilot?.cogBalance ?? 0) >= listing.priceInCogs;
  }

  listingQuantityOptions(itemQuantity: number): number[] {
    const upperBound = Math.max(1, Math.min(itemQuantity, 100));
    return Array.from({ length: upperBound }, (_, idx) => idx + 1);
  }

  onListingGearChanged(): void {
    this.ensureListingFormTargets();
  }

  sessionDurationLabel(session: CogSession): string {
    if (session.isOpen) {
      const startedAt = this.toUtcMilliseconds(session.cogInAtUtc);
      if (startedAt === null) {
        return 'In progress';
      }

      const elapsedMinutes = Math.max(0, Math.floor((Date.now() - startedAt) / 60000));
      return `${this.formatDurationMinutes(elapsedMinutes)} elapsed`;
    }

    return this.formatDurationMinutes(session.durationMinutes);
  }

  onCogHandlePointerDown(event: PointerEvent): void {
    if (!this.showCogCheckOverlay || this.cogCheckSpinCount >= this.cogCheckSpinsRequired) {
      return;
    }

    const handle = event.currentTarget as HTMLElement | null;
    if (!handle) {
      return;
    }

    handle.setPointerCapture(event.pointerId);
    this.cogHandleActivePointerId = event.pointerId;
    this.cogHandleLastPointerAngle = this.pointerAngle(event, handle);
    this.isCogHandleDragging = true;
    this.ensureCogCheckAlarmPlayback();
  }

  onCogHandlePointerMove(event: PointerEvent): void {
    if (!this.showCogCheckOverlay
      || !this.isCogHandleDragging
      || this.cogHandleActivePointerId !== event.pointerId
      || this.cogCheckSpinCount >= this.cogCheckSpinsRequired) {
      return;
    }

    const handle = event.currentTarget as HTMLElement | null;
    const previousAngle = this.cogHandleLastPointerAngle;
    if (!handle || previousAngle === null) {
      return;
    }

    const currentAngle = this.pointerAngle(event, handle);
    const delta = this.normalizeAngleDelta(currentAngle - previousAngle);
    this.cogHandleLastPointerAngle = currentAngle;

    if (Math.abs(delta) < 0.5) {
      return;
    }

    if (this.cogCheckDirection === 0) {
      this.cogCheckDirection = delta >= 0 ? 1 : -1;
    }

    const directionalDelta = delta * this.cogCheckDirection;
    this.cogCheckProgressDegrees = Math.max(0, this.cogCheckProgressDegrees + directionalDelta);
    this.cogCheckHandleRotation += delta;

    const completedSpins = Math.floor(this.cogCheckProgressDegrees / 360);
    this.cogCheckSpinCount = Math.min(
      this.cogCheckSpinsRequired,
      Math.max(this.cogCheckSpinCount, completedSpins)
    );

    event.preventDefault();
  }

  onCogHandlePointerUp(event: PointerEvent): void {
    if (this.cogHandleActivePointerId !== event.pointerId) {
      return;
    }

    const handle = event.currentTarget as HTMLElement | null;
    if (handle?.hasPointerCapture(event.pointerId)) {
      handle.releasePointerCapture(event.pointerId);
    }

    this.releaseCogHandlePointerState();
  }

  async submitCogCheck(): Promise<void> {
    if (!this.cogCheckStatus?.requiresCogCheck) {
      this.errorMessage = 'No cog check is currently active.';
      return;
    }

    if (this.cogCheckSpinCount < this.cogCheckSpinsRequired) {
      this.errorMessage = `Spin the handle ${this.cogCheckSpinsRequired} times before submitting cog check.`;
      return;
    }

    this.errorMessage = '';

    try {
      const status = await firstValueFrom(this.api.completeCogCheck(this.cogCheckSpinCount));
      this.applyCogCheckStatus(status, true);

      if (!status.requiresCogCheck) {
        this.infoMessage = 'Cog check passed. Shift remains active.';
        await this.refreshAll();
      } else {
        this.infoMessage = 'Cog check logged. Another check is already due.';
      }
    } catch (error) {
      this.captureError(error, 'Cog check submission failed.');
    }
  }

  async saveWarningInterval(): Promise<void> {
    const minutes = Math.floor(this.warningIntervalForm.warningIntervalMinutes);
    if (minutes < 5 || minutes > 720) {
      this.errorMessage = 'Warning interval must be between 5 and 720 minutes.';
      return;
    }

    this.errorMessage = '';

    try {
      const settings = await firstValueFrom(this.api.updateCogWarningInterval({ warningIntervalMinutes: minutes }));
      this.cogRuntimeSettings = settings;
      this.warningIntervalForm.warningIntervalMinutes = settings.warningIntervalMinutes;
      this.infoMessage = `Global cog-check interval set to ${settings.warningIntervalMinutes} minute(s).`;
      await this.refreshCogCheckStatus();
    } catch (error) {
      this.captureError(error, 'Warning interval update failed.');
    }
  }

  async grantCogs(): Promise<void> {
    if (!this.grantCogsForm.userAccountId || this.grantCogsForm.amount < 1) {
      this.errorMessage = 'Disbursement requires a designated recipient and a positive cog amount.';
      return;
    }

    this.errorMessage = '';

    try {
      await firstValueFrom(this.api.grantCogs(this.grantCogsForm));
      this.infoMessage = 'Cogs disbursed. The recipient has been notified by the usual channels.';
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Disbursement denied. The cog reserve has flagged this transaction.');
    }
  }

  async grantGear(): Promise<void> {
    if (!this.grantGearForm.userAccountId || !this.grantGearForm.gearItemId) {
      this.errorMessage = 'Allocation requires both a recipient and a gear specification.';
      return;
    }

    this.errorMessage = '';

    try {
      await firstValueFrom(this.api.grantGear(this.grantGearForm));
      this.infoMessage = 'Gear allocated and transferred. Inventory records updated.';
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Gear allocation rejected by supply chain authority.');
    }
  }

  startEditGearItem(item: StoreItem): void {
    this.editingGearItemId = item.gearItemId;
    this.gearForm = {
      name: item.name,
      description: item.description ?? '',
      gearType: item.gearType,
      costInCogs: item.costInCogs,
      stockQuantity: item.stockQuantity ?? null,
      isActive: item.isActive,
      flavorText: item.flavorText ?? ''
    };
    this.infoMessage = 'Gear loaded for editing.';
  }

  cancelGearEdit(): void {
    this.editingGearItemId = null;
    this.gearForm = this.createEmptyGearForm();
  }

  async saveGearItem(): Promise<void> {
    this.errorMessage = '';

    const payload = {
      ...this.gearForm,
      name: this.gearForm.name.trim(),
      gearType: this.gearForm.gearType.trim(),
      description: this.normalizeText(this.gearForm.description),
      flavorText: this.normalizeText(this.gearForm.flavorText),
      stockQuantity: this.normalizeStock(this.gearForm.stockQuantity)
    };

    if (!payload.name || !payload.gearType) {
      this.errorMessage = 'Gear name and type are required fields.';
      return;
    }

    try {
      if (this.editingGearItemId === null) {
        await firstValueFrom(this.api.createGearItem(payload));
        this.infoMessage = 'New gear commissioned and entered into the official registry.';
      } else {
        await firstValueFrom(this.api.updateGearItem(this.editingGearItemId, payload));
        this.infoMessage = 'Gear updated. The economy adjusts.';
      }

      this.cancelGearEdit();
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'The registry has refused this specification. Review and resubmit.');
    }
  }

  private async refreshCogCheckStatus(): Promise<void> {
    const status = await firstValueFrom(this.api.getCogCheckStatus());
    this.applyCogCheckStatus(status, true);
  }

  private applyCogCheckStatus(status: CogCheckStatus, shouldNotifyAutoCogOut: boolean): void {
    const hadOpenSession = this.cogCheckStatus?.hasOpenSession ?? false;
    this.cogCheckStatus = status;

    const overlayWasVisible = this.showCogCheckOverlay;
    this.showCogCheckOverlay = status.hasOpenSession && status.requiresCogCheck;
    this.setCogCheckPageLock(this.showCogCheckOverlay);

    if (!overlayWasVisible && this.showCogCheckOverlay) {
      this.resetCogCheckHandle();
      this.hasPlayedCogCheckAlarmForCurrentOverlay = false;
      this.startCogCheckAlarm();
    }

    if (!this.showCogCheckOverlay) {
      this.resetCogCheckHandle();
      this.stopCogCheckAlarm();
    } else if (this.isCogCheckAlarmBlockedByAutoplay) {
      this.ensureCogCheckAlarmPlayback();
    }

    if (shouldNotifyAutoCogOut
      && status.autoCoggedOutNoPayout
      && status.cogSessionId
      && this.lastNotifiedAutoCogOutSessionId !== status.cogSessionId) {
      this.lastNotifiedAutoCogOutSessionId = status.cogSessionId;
      this.infoMessage = 'Auto cog-out triggered after missed cog check. No payout issued for that shift.';
    }

    if (hadOpenSession && !status.hasOpenSession) {
      void this.loadMarketplaceAndSessions();
    }
  }

  private startCogCheckPolling(): void {
    this.stopCogCheckPolling();
    this.cogCheckPollHandle = setInterval(() => {
      if (!this.isAuthenticated) {
        return;
      }

      void this.pollCogCheckStatus();
    }, 30000);
  }

  private stopCogCheckPolling(): void {
    if (this.cogCheckPollHandle === null) {
      return;
    }

    clearInterval(this.cogCheckPollHandle);
    this.cogCheckPollHandle = null;
  }

  private async pollCogCheckStatus(): Promise<void> {
    try {
      const status = await firstValueFrom(this.api.getCogCheckStatus());
      this.applyCogCheckStatus(status, true);
    } catch {
      // Poll failures are ignored and retried next interval.
    }
  }

  private resetCogCheckHandle(): void {
    this.releaseCogHandlePointerState();
    this.cogCheckSpinCount = 0;
    this.cogCheckHandleRotation = 0;
    this.cogCheckProgressDegrees = 0;
    this.cogCheckDirection = 0;
  }

  private releaseCogHandlePointerState(): void {
    this.isCogHandleDragging = false;
    this.cogHandleActivePointerId = null;
    this.cogHandleLastPointerAngle = null;
  }

  private pointerAngle(event: PointerEvent, element: HTMLElement): number {
    const rect = element.getBoundingClientRect();
    const centerX = rect.left + (rect.width / 2);
    const centerY = rect.top + (rect.height / 2);
    return Math.atan2(event.clientY - centerY, event.clientX - centerX) * (180 / Math.PI);
  }

  private normalizeAngleDelta(delta: number): number {
    if (delta > 180) {
      return delta - 360;
    }

    if (delta < -180) {
      return delta + 360;
    }

    return delta;
  }

  private initializeCogCheckAlarm(): void {
    const alarm = new Audio('/assets/cog-check.mp3');
    alarm.loop = false;
    alarm.preload = 'auto';
    alarm.volume = 0.85;
    this.cogCheckAlarmAudio = alarm;
  }

  private startCogCheckAlarm(): void {
    if (!this.cogCheckAlarmAudio
      || !this.showCogCheckOverlay
      || this.hasPlayedCogCheckAlarmForCurrentOverlay) {
      return;
    }

    void this.cogCheckAlarmAudio.play()
      .then(() => {
        this.isCogCheckAlarmBlockedByAutoplay = false;
        this.hasPlayedCogCheckAlarmForCurrentOverlay = true;
      })
      .catch(() => {
        this.isCogCheckAlarmBlockedByAutoplay = true;
      });
  }

  private ensureCogCheckAlarmPlayback(): void {
    if (!this.showCogCheckOverlay || !this.cogCheckAlarmAudio) {
      return;
    }

    if (this.hasPlayedCogCheckAlarmForCurrentOverlay && !this.isCogCheckAlarmBlockedByAutoplay) {
      return;
    }

    this.startCogCheckAlarm();
  }

  private stopCogCheckAlarm(): void {
    if (!this.cogCheckAlarmAudio) {
      return;
    }

    this.cogCheckAlarmAudio.pause();
    this.cogCheckAlarmAudio.currentTime = 0;
    this.isCogCheckAlarmBlockedByAutoplay = false;
    this.hasPlayedCogCheckAlarmForCurrentOverlay = false;
  }

  private setCogCheckPageLock(isLocked: boolean): void {
    this.documentRef.body.classList.toggle('cog-check-lock', isLocked);
  }

  private async loadAdminPanel(): Promise<void> {
    const data = await firstValueFrom(
      forkJoin({
        users: this.api.getAdminUsers(),
        gearItems: this.api.getAdminGearItems(true),
        cogSettings: this.api.getCogRuntimeSettings()
      })
    );

    this.adminUsers = data.users;
    this.adminGearItems = data.gearItems;
    this.cogRuntimeSettings = data.cogSettings;
    this.warningIntervalForm.warningIntervalMinutes = data.cogSettings.warningIntervalMinutes;

    if (this.adminUsers.length > 0) {
      if (!this.grantCogsForm.userAccountId) {
        this.grantCogsForm.userAccountId = this.adminUsers[0].userAccountId;
      }

      if (!this.grantGearForm.userAccountId) {
        this.grantGearForm.userAccountId = this.adminUsers[0].userAccountId;
      }
    }

    if (this.adminGearItems.length > 0 && !this.grantGearForm.gearItemId) {
      this.grantGearForm.gearItemId = this.adminGearItems[0].gearItemId;
    }
  }

  private async loadMarketplaceAndSessions(): Promise<void> {
    const data = await firstValueFrom(
      forkJoin({
        listings: this.api.getMarketplaceListings(),
        sessions: this.api.getCogSessionHistory(100)
      })
    );

    this.marketplaceListings = data.listings;
    this.cogSessionHistory = data.sessions;
  }

  private ensureListingFormTargets(): void {
    const inventory = this.pilot?.inventory ?? [];

    if (inventory.length === 0) {
      this.listingForm.gearItemId = 0;
      this.listingForm.quantity = 1;
      return;
    }

    const hasSelected = inventory.some(x => x.gearItemId === this.listingForm.gearItemId);
    if (!hasSelected) {
      this.listingForm.gearItemId = inventory[0].gearItemId;
    }

    const selected = this.selectedListingItem;
    if (selected && this.listingForm.quantity > selected.quantity) {
      this.listingForm.quantity = selected.quantity;
    }

    if (!selected || this.listingForm.quantity < 1) {
      this.listingForm.quantity = 1;
    }
  }

  private normalizeText(value: string | null | undefined): string | null {
    const trimmed = value?.trim();
    return trimmed ? trimmed : null;
  }

  private normalizeStock(value: number | null | undefined): number | null {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return null;
    }

    return Math.max(0, Math.floor(value));
  }

  private captureError(error: unknown, fallbackMessage: string): void {
    this.infoMessage = '';

    if (error instanceof HttpErrorResponse) {
      const apiMessage = (error.error as { message?: string } | null)?.message;
      this.errorMessage = apiMessage ?? fallbackMessage;
      return;
    }

    this.errorMessage = fallbackMessage;
  }

  private formatDurationMinutes(durationMinutes: number | null | undefined): string {
    if (durationMinutes === null || durationMinutes === undefined) {
      return 'In progress';
    }

    const normalizedMinutes = Math.max(0, Math.floor(durationMinutes));
    const hours = Math.floor(normalizedMinutes / 60);
    const minutes = normalizedMinutes % 60;

    if (hours === 0) {
      return `${minutes}m`;
    }

    if (minutes === 0) {
      return `${hours}h`;
    }

    return `${hours}h ${minutes}m`;
  }

  private toUtcMilliseconds(value: string | null | undefined): number | null {
    if (!value) {
      return null;
    }

    const hasOffset = /([zZ]|[+-]\d{2}:\d{2})$/.test(value);
    const normalized = hasOffset ? value : `${value}Z`;
    const parsed = Date.parse(normalized);
    return Number.isNaN(parsed) ? null : parsed;
  }

  private beginDisplayNameOnboarding(profile: UserProfile): void {
    this.isAuthenticated = true;
    this.requiresDisplayName = true;
    this.dashboard = {
      pilot: profile,
      storeFront: []
    };
    this.displayNameDraft = '';
    this.marketplaceListings = [];
    this.cogSessionHistory = [];
    this.cogCheckStatus = null;
    this.showCogCheckOverlay = false;
    this.resetCogCheckHandle();
    this.stopCogCheckAlarm();
    this.setCogCheckPageLock(false);
    this.stopCogCheckPolling();
    this.adminUsers = [];
    this.adminGearItems = [];
    this.cogRuntimeSettings = null;
    this.activePage = 'pilot';
    this.infoMessage = 'Choose your display name to enter the Cog Slop economy.';
  }

  private applyUnauthenticatedState(message: string): void {
    this.isAuthenticated = false;
    this.requiresDisplayName = false;
    this.displayNameDraft = '';
    this.dashboard = null;
    this.marketplaceListings = [];
    this.cogSessionHistory = [];
    this.cogCheckStatus = null;
    this.showCogCheckOverlay = false;
    this.resetCogCheckHandle();
    this.stopCogCheckAlarm();
    this.setCogCheckPageLock(false);
    this.stopCogCheckPolling();
    this.adminUsers = [];
    this.adminGearItems = [];
    this.cogRuntimeSettings = null;
    this.activePage = 'pilot';
    this.infoMessage = message;
  }

  private hasConfiguredDisplayName(value: string | null | undefined): boolean {
    const trimmed = value?.trim();
    return Boolean(trimmed);
  }

  private createEmptyGearForm(): UpsertGearPayload {
    return {
      name: '',
      description: '',
      gearType: 'Trinket',
      costInCogs: 10,
      stockQuantity: null,
      isActive: true,
      flavorText: 'Manufactured under license from the Central Cog Authority.'
    };
  }

  private createEmptyCraftForm(): CraftGearPayload {
    return {
      name: '',
      description: '',
      gearType: 'Custom',
      craftingCostInCogs: 35,
      flavorText: 'One-off forge run from my personal cog press.'
    };
  }
}
