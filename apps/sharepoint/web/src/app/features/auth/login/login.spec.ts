import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Login } from './login';
import { AuthService } from '../../../core/auth/auth.service';

describe('Login', () => {
    let authServiceStub: { login: ReturnType<typeof vi.fn> };
    let router: Router;

    beforeEach(async () => {
        authServiceStub = { login: vi.fn() };

        await TestBed.configureTestingModule({
            imports: [Login],
            providers: [provideRouter([]), { provide: AuthService, useValue: authServiceStub }],
        }).compileComponents();

        router = TestBed.inject(Router);
        vi.spyOn(router, 'navigate').mockResolvedValue(true);
    });

    it('form is invalid when empty and valid once filled', () => {
        const fixture = TestBed.createComponent(Login);
        const component = fixture.componentInstance;

        expect(component.form.invalid).toBe(true);

        component.form.setValue({ email: 'jane@example.com', password: 'password123' });
        expect(component.form.valid).toBe(true);
    });

    it('successful submit calls AuthService.login and navigates to /', async () => {
        authServiceStub.login.mockResolvedValue(undefined);
        const fixture = TestBed.createComponent(Login);
        const component = fixture.componentInstance;

        component.form.setValue({ email: 'jane@example.com', password: 'password123' });
        await component.onSubmit();

        expect(authServiceStub.login).toHaveBeenCalledWith({
            email: 'jane@example.com',
            password: 'password123',
        });
        expect(router.navigate).toHaveBeenCalledWith(['/']);
        expect(component.errorMessage()).toBeNull();
    });

    it('failed submit renders the error message and does not navigate', async () => {
        authServiceStub.login.mockRejectedValue(
            new HttpErrorResponse({
                status: 401,
                error: { message: 'Invalid email or password.' },
            }),
        );
        const fixture = TestBed.createComponent(Login);
        const component = fixture.componentInstance;

        component.form.setValue({ email: 'jane@example.com', password: 'wrong-password' });
        await component.onSubmit();

        expect(component.errorMessage()).toBe('Invalid email or password.');
        expect(router.navigate).not.toHaveBeenCalled();
    });
});
