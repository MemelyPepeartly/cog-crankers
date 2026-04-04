import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AppComponent } from './app.component';
import { UserProfile } from './models/economy.models';
import { EconomyApiService } from './services/economy-api.service';

describe('AppComponent display name flow', () => {
  let apiSpy: jasmine.SpyObj<EconomyApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj<EconomyApiService>('EconomyApiService', [
      'getCurrentUserProfile',
      'getDashboard',
      'updateDisplayName'
    ]);

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        { provide: EconomyApiService, useValue: apiSpy }
      ]
    }).compileComponents();
  });

  it('forces onboarding when profile display name is blank', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    const component = fixture.componentInstance;
    apiSpy.getCurrentUserProfile.and.returnValue(of(createUserProfile('')));

    await component.refreshAll();

    expect(component.isAuthenticated).toBeTrue();
    expect(component.requiresDisplayName).toBeTrue();
    expect(component.infoMessage).toContain('Choose your display name');
    expect(apiSpy.getDashboard).not.toHaveBeenCalled();
  });

  it('handles dashboard 401 by resetting to unauthenticated state', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    const component = fixture.componentInstance;
    apiSpy.getCurrentUserProfile.and.returnValue(of(createUserProfile('Cog Citizen')));
    apiSpy.getDashboard.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' })));

    await component.refreshAll();

    expect(component.isAuthenticated).toBeFalse();
    expect(component.requiresDisplayName).toBeFalse();
    expect(component.infoMessage).toContain('No active session');
    expect(component.errorMessage).toBe('');
  });

  it('saves display name through API and refreshes', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    const component = fixture.componentInstance;
    component.requiresDisplayName = true;
    component.displayNameDraft = '  Gear Baron  ';
    apiSpy.updateDisplayName.and.returnValue(of(createUserProfile('Gear Baron')));
    const refreshSpy = spyOn(component, 'refreshAll').and.resolveTo();

    await component.saveDisplayName();

    expect(apiSpy.updateDisplayName).toHaveBeenCalledWith({ displayName: 'Gear Baron' });
    expect(component.displayNameDraft).toBe('Gear Baron');
    expect(component.infoMessage).toContain('Display name calibrated');
    expect(refreshSpy).toHaveBeenCalled();
  });
});

function createUserProfile(displayName: string): UserProfile {
  return {
    userAccountId: 1,
    displayName,
    email: 'pilot@cogslop.test',
    avatarUrl: null,
    cogBalance: 100,
    roles: ['CogUser'],
    inventory: [],
    isAdmin: false
  };
}
