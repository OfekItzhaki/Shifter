# Requirements Document

## Introduction

Custom error pages for the Rolduler scheduling application. Currently, when HTTP errors occur (expired tokens, forbidden resources, missing pages, server failures), users see raw browser error pages or unhandled exceptions. This feature replaces those with branded, user-friendly error pages that guide users toward resolution — such as logging in again, navigating home, or contacting support.

## Glossary

- **Error_Page**: A dedicated Next.js page component that renders when a specific HTTP error condition is detected, displaying a branded UI with contextual messaging and navigation options.
- **Error_Boundary**: A React class component that catches unhandled JavaScript exceptions in its child component tree and renders a fallback UI instead of crashing the page.
- **Axios_Interceptor**: The response interceptor configured on the shared axios client that detects HTTP error status codes from API responses and triggers appropriate client-side error handling.
- **App_Shell**: The top-level layout and provider structure of the Next.js application (layout.tsx, providers.tsx) that wraps all page content.
- **Branding**: The consistent visual identity of the Rolduler/Shifter application including logo, color palette, typography, and tone of voice.

## Requirements

### Requirement 1: 404 Not Found Page

**User Story:** As a user, I want to see a helpful branded page when I navigate to a URL that does not exist, so that I understand the page is missing and can navigate back to a valid location.

#### Acceptance Criteria

1. WHEN a user navigates to a route that does not match any defined page, THE Error_Page SHALL return HTTP status code 404 and render a page displaying the application name and logo.
2. WHEN the 404 Error_Page is displayed, THE Error_Page SHALL show a heading element (h1) containing text that indicates the requested page was not found.
3. WHEN the 404 Error_Page is displayed, THE Error_Page SHALL provide a visible link that navigates the user to the application root path ("/").
4. THE Error_Page SHALL display all user-visible text in the application's current locale (English, Hebrew, or Russian) using next-intl, with no untranslated strings rendered.
5. WHILE the active locale is Hebrew, THE Error_Page SHALL render with a right-to-left layout direction (dir="rtl").

### Requirement 2: 401 Unauthorized Page

**User Story:** As a user, I want to see a branded page when my session has expired and token refresh has failed, so that I understand I need to log in again.

#### Acceptance Criteria

1. WHEN the Axios_Interceptor detects a 401 response and token refresh fails (refresh endpoint returns an error or no refresh token exists in localStorage), THE Axios_Interceptor SHALL redirect the user to a dedicated 401 Unauthorized Error_Page.
2. WHEN the 401 Error_Page is displayed, THE Error_Page SHALL display a visible text message indicating that the user's session has expired and they need to log in again.
3. WHEN the 401 Error_Page is displayed, THE Error_Page SHALL provide a visually distinct link to the login page that is rendered as a button-styled element or anchor occupying at least 44×44 CSS pixels of tap target area.
4. WHEN the 401 Error_Page is navigated to, THE Error_Page SHALL remove the "access_token" and "refresh_token" entries from localStorage before displaying page content to the user.
5. IF a user navigates directly to the 401 Error_Page while valid tokens exist in localStorage, THEN THE Error_Page SHALL still clear the stored tokens and display the session-expired message.

### Requirement 3: 403 Forbidden Page

**User Story:** As a user, I want to see a clear message when I attempt to access a resource I do not have permission for, so that I understand the restriction and can take appropriate action.

#### Acceptance Criteria

1. WHEN an API response returns a 403 status code, THE Error_Page SHALL render a Forbidden page that displays the application logo and uses the application's standard layout and color scheme.
2. WHEN the 403 Error_Page is displayed, THE Error_Page SHALL display a heading indicating access is forbidden and a message informing the user that they do not have permission to access the requested resource.
3. WHEN the 403 Error_Page is displayed, THE Error_Page SHALL provide a navigation link to the home page and a "go back" action that navigates to the previous page in browser history.
4. THE Error_Page SHALL not expose any internal permission details, role names, policy identifiers, or raw API error response bodies to the user.
5. WHEN the 403 Error_Page is displayed, THE Error_Page SHALL render all text and interactive elements with sufficient contrast and keyboard-navigable focus states conforming to WCAG 2.1 Level AA.

### Requirement 4: 500 Internal Server Error Page

**User Story:** As a user, I want to see a friendly page when the server encounters an unexpected error, so that I know the issue is not on my end and can retry or contact support.

#### Acceptance Criteria

1. WHEN an unhandled server-side exception occurs during page rendering, THE Error_Page SHALL render a 500 Internal Server Error page that displays the application logo and uses the application's standard layout.
2. WHEN the 500 Error_Page is displayed, THE Error_Page SHALL inform the user that an unexpected error occurred on the server.
3. WHEN the 500 Error_Page is displayed, THE Error_Page SHALL provide a button that triggers a full page reload of the current URL.
4. WHEN the 500 Error_Page is displayed, THE Error_Page SHALL provide a link that navigates to the application root path.
5. THE Error_Page SHALL not display stack traces, exception messages, or any internal server details to the user.
6. IF the server-side rendering pipeline fails entirely, THEN THE Error_Page SHALL render client-side without depending on server-fetched data.

### Requirement 5: Client-Side Runtime Error Handling

**User Story:** As a user, I want to see a helpful page when an unexpected client-side error occurs, so that I can recover without seeing a blank screen or cryptic error.

#### Acceptance Criteria

1. WHEN an unhandled JavaScript exception occurs within the App_Shell, THE Error_Boundary SHALL catch the exception and render a fallback UI that includes the application name or logo, a heading indicating an error occurred, and a brief user-friendly message.
2. WHEN the Error_Boundary fallback is displayed, THE Error_Boundary SHALL provide a button that reloads the current page via a full browser navigation.
3. WHEN the Error_Boundary fallback is displayed, THE Error_Boundary SHALL provide a link that navigates to the application root path ("/").
4. WHEN the Error_Boundary catches an exception, THE Error_Boundary SHALL log the error object and component stack trace to the browser console.
5. IF the application is running in a production build, THEN THE Error_Boundary SHALL not display the exception message, stack trace, or component tree information to the user.
6. IF the application is running in a development build, THEN THE Error_Boundary SHALL display the exception message in the fallback UI to aid debugging.

### Requirement 6: Consistent Visual Design

**User Story:** As a product owner, I want all error pages to share a consistent visual design aligned with the application brand, so that users have a cohesive experience even during error states.

#### Acceptance Criteria

1. THE Error_Page SHALL use the application's existing Tailwind utility classes and CSS variables defined in globals.css for all colors, typography, and spacing, with no inline hardcoded color values outside the design system.
2. THE Error_Page SHALL display the ShifterLogo component at a size of 40px on every error page, centered above the error message content.
3. THE Error_Page SHALL apply dark-mode-aware classes (Tailwind `dark:` variants or CSS variables scoped under `.dark`) for all text, background, and border colors so that no element becomes invisible or illegible when the ThemeProvider resolves to dark mode.
4. THE Error_Page SHALL render without horizontal overflow, without truncation of actionable text or links, and with all interactive elements maintaining a minimum touch target of 44×44px on viewport widths from 320px to 1920px.
5. THE Error_Page SHALL meet WCAG 2.1 Level AA contrast requirements (minimum 4.5:1 for normal text, 3:1 for large text) for all text elements in both light and dark modes.
6. THE Error_Page SHALL use a single shared layout component across all error types (401, 403, 404, 500, and client-side error boundary) to ensure consistent placement of the logo, heading, message, and navigation actions.

### Requirement 7: API Error Interception and Routing

**User Story:** As a developer, I want a centralized mechanism to detect API error responses and route users to the appropriate error page, so that error handling is consistent across the application.

#### Acceptance Criteria

1. WHEN the Axios_Interceptor receives a 403 response from any API call, THE Axios_Interceptor SHALL redirect the user to the 403 Error_Page and reject the promise with the original error so that calling code does not hang on an unresolved promise.
2. WHEN the Axios_Interceptor receives a response with status code 500, 502, 503, or 504 from any API call, THE Axios_Interceptor SHALL redirect the user to the 500 Error_Page and reject the promise with the original error.
3. WHEN the Axios_Interceptor receives a 404 response from an API call, THE Axios_Interceptor SHALL reject the promise with the original error response (including status and body) without redirecting, allowing page-level components to handle missing resources contextually.
4. WHEN the Axios_Interceptor redirects to an error page, THE Axios_Interceptor SHALL append a query parameter named "from" containing the current browser page path (window.location.pathname) to the error page URL, enabling a "go back" action.
5. IF multiple API calls fail concurrently with redirect-triggering status codes (403 or 5xx), THEN THE Axios_Interceptor SHALL perform only one redirect for the first error received and suppress subsequent redirects while navigation is in progress.
6. WHEN the Axios_Interceptor processes error responses, THE Axios_Interceptor SHALL evaluate status codes in the following order: 401 (existing refresh logic) first, then 403, then 5xx — ensuring that a 403 or 5xx returned during a 401 token-refresh attempt is handled by the appropriate error page redirect rather than silently swallowed.
