# Changelog

All notable changes to this project should be documented in this file.

## Unreleased

- Added administrator/user role control with a user management page for creating users, changing roles, disabling accounts, and resetting passwords.
- Added resource ownership for frp tunnels and HTTPS proxy rules: all users can view resources, normal users can manage only their own resources, and administrators can manage all resources.
- Restricted global operations such as frpc configuration, backup/restore, audit logs, and sync-from-frpc to administrators.
- Added enable-time port conflict checks while still allowing duplicate saved configurations: enabled HTTPS proxy rules cannot share a listen port, and enabled TCP/UDP tunnels cannot share the same remote port.
- Added Wake-on-LAN MAC address management, including host naming, automatic MAC address collection, and wake actions from the address book.
- Enhanced scheduled Wake-on-LAN tasks with daily, selected weekdays, and one-time specific-date modes.
- Changed backup restore to merge imported data without deleting existing tunnels, HTTPS proxy rules, or MAC address records.
- Included Wake-on-LAN MAC address records and scheduled wake tasks in configuration backups.
- Added database schema compatibility version tracking to avoid running compatibility initialization on every startup.
- Fixed publish output folder handling so local `publish` output is not included again during backend publish.
- Improved tunnel management action button alignment.
- Added Wake-on-LAN records with wake-again support.
- Added daily scheduled Wake-on-LAN tasks.
- Added lightweight HTTPS reverse proxy rules with default certificate, IIS PFX/P12 certificate, and Nginx PEM+KEY certificate support.
- Added English README documentation.
- Added Docker Hub image build/push scripts.
- Added Docker Hub repository description update scripts.
- Added first-run setup wizard, audit logs, health checks, Docker Compose, and backup/restore support.
- Added optional MySQL support while keeping SQLite as the default database.
