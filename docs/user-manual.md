# Gift of the Givers Disaster Relief System — User Manual

Staff / donor / volunteer guide for the live site. Written for non-technical Gift of the Givers staff.

- **Live URL:** https://gift-of-the-givers-prototype-aweeh5f7g2czemdf.southafricanorth-01.azurewebsites.net
- **Builds documented:** 20261006.6 and 20261006.7 (footer shows the build number)
- **Captured:** 6–7 October 2026 in Google Chrome
- **Full Word document:** `APPR6312 POE Part 3 - User Manual.docx` (29 captioned figures)

## What the manual covers

1. About this manual (audience, live vs local screenshots, Manual Demo data)
2. Getting started (opening the site, accessibility)
3. Accounts: guest use, register, sign in as donor, sign in as employee, sign out
4. Employee tasks: post a relief operation, update status, review volunteers
5. Donations: guest, once-off, recurring (weekly/monthly/quarterly/yearly), other currencies, cancel a schedule
6. Tax certificates (print / save as PDF)
7. Volunteer registration
8. Troubleshooting / FAQ
9. Getting help

## Screenshot sources

| Screens | Source |
|---|---|
| Home, donate, donor dashboard, tax certificate, volunteer, errors (L figures) | Live Azure site in Chrome |
| Employee sign-in, dashboard, operations, volunteer review (E figures) | Local copy of the same code — live `CK_Users_Role` only allows Administrator / Volunteer / Donor |

Test records are labelled **Manual Demo** (guest ZAR 250, Manual Demo Donor USD 100 + EUR 50 monthly then cancelled, Manual Demo Volunteer application).

## Related commits

- `d4eb401` Add recurring donations and a printable tax certificate
- `2efa812` Make the donation frequency optional on post
- `e2a0606` Widen and tidy the volunteer form
