| Test assembly | Before | After | Removed | Added |
|---|---:|---:|---:|---:|
| TicketDesk.Data.Tests | 28 | 28 | 0 | 0 |
| TicketDesk.Database.Tests | 95 | 95 | 1 | 1 |
| TicketDesk.Identity.Tests | 38 | 38 | 6 | 6 |
| TicketDesk.Modules.Tests | 275 | 275 | 0 | 0 |
| TicketDesk.Replay.Tests | 101 | 101 | 0 | 0 |
| TicketDesk.Seed.Tests | 20 | 20 | 0 | 0 |
| TicketDesk.Web.Tests | 178 | 166 | 44 | 32 |
| TicketDesk.Worker.Tests | 6 | 6 | 0 | 0 |
| **total** | **741** | **729** | 51 | 39 |

Removed (by class; a theory counts once per case):

- `TicketDesk.Database.Tests.ExpandTests`: 1
- `TicketDesk.Identity.Tests.PasswordCompatibilityTests`: 4
- `TicketDesk.Identity.Tests.UserStoreTests`: 2
- `TicketDesk.Web.Tests.AngularPagesTests`: 1
- `TicketDesk.Web.Tests.GatewayEdgeTests`: 6
- `TicketDesk.Web.Tests.GatewayTests`: 19
- `TicketDesk.Web.Tests.HealthTests`: 2
- `TicketDesk.Web.Tests.MigrationRoutingTests`: 8
- `TicketDesk.Web.Tests.RouteInventoryTests`: 2
- `TicketDesk.Web.Tests.SharedSignInTests`: 6

Added (by class):

- `TicketDesk.Database.Tests.ExpandTests`: 1
- `TicketDesk.Identity.Tests.PasswordCompatibilityTests`: 4
- `TicketDesk.Identity.Tests.UserStoreTests`: 2
- `TicketDesk.Web.Tests.AngularPagesTests`: 1
- `TicketDesk.Web.Tests.HealthTests`: 1
- `TicketDesk.Web.Tests.MigrationRoutingTests`: 4
- `TicketDesk.Web.Tests.RetiredRoutesTests`: 15
- `TicketDesk.Web.Tests.RouteInventoryTests`: 3
- `TicketDesk.Web.Tests.SignInTests`: 8

Removed tests:

- `TicketDesk.Database.Tests.ExpandTests.EF_Core_saves_tickets_on_the_table_with_the_trigger_and_OUTPUT_without_INTO_is_refused`
- `TicketDesk.Identity.Tests.PasswordCompatibilityTests.Default_V3_mode_would_rehash_legacy_hashes`
- `TicketDesk.Identity.Tests.PasswordCompatibilityTests.Host_configures_IdentityV2_compatibility_mode`
- `TicketDesk.Identity.Tests.PasswordCompatibilityTests.New_hashes_are_v2_and_the_legacy_algorithm_verifies_them`
- `TicketDesk.Identity.Tests.PasswordCompatibilityTests.Seeder_v2_hash_verifies_without_rehash`
- `TicketDesk.Identity.Tests.UserStoreTests.New_and_changed_passwords_are_stored_as_v2_hashes`
- `TicketDesk.Identity.Tests.UserStoreTests.Seeded_v2_hash_signs_in_and_stays_v2`
- `TicketDesk.Web.Tests.AngularPagesTests.Switching_back_gives_the_server_page_then_the_legacy_app_without_a_restart`
- `TicketDesk.Web.Tests.GatewayEdgeTests.Gateway_can_be_switched_off`
- `TicketDesk.Web.Tests.GatewayEdgeTests.Legacy_outage_is_a_502_while_migrated_endpoints_keep_working`
- `TicketDesk.Web.Tests.GatewayEdgeTests.Remote_app_authentication_is_off_by_default`
- `TicketDesk.Web.Tests.GatewayEdgeTests.Remote_app_authentication_when_enabled_asks_the_legacy_app_only_for_migrated_endpoints`
- `TicketDesk.Web.Tests.GatewayEdgeTests.Remote_app_without_an_api_key_refuses_to_start`
- `TicketDesk.Web.Tests.GatewayEdgeTests.Self_signed_legacy_certificate_is_refused_unless_pinned`
- `TicketDesk.Web.Tests.GatewayTests.Absolute_legacy_redirect_becomes_gateway_relative`
- `TicketDesk.Web.Tests.GatewayTests.Antiforgery_sign_in_round_trip_works_through_the_gateway`
- `TicketDesk.Web.Tests.GatewayTests.Error_statuses_pass_through_with_their_bodies(path: "/boom", status: 500, body: "Server Error in '/' Application.")`
- `TicketDesk.Web.Tests.GatewayTests.Error_statuses_pass_through_with_their_bodies(path: "/empty404", status: 404, body: "")`
- `TicketDesk.Web.Tests.GatewayTests.Error_statuses_pass_through_with_their_bodies(path: "/missing", status: 404, body: "legacy 404 page")`
- `TicketDesk.Web.Tests.GatewayTests.Get_passes_status_headers_and_body_through`
- `TicketDesk.Web.Tests.GatewayTests.Large_download_streams_intact`
- `TicketDesk.Web.Tests.GatewayTests.Large_upload_streams_intact_past_the_kestrel_default_limit`
- `TicketDesk.Web.Tests.GatewayTests.Migrated_endpoints_win_and_api_paths_are_never_proxied(path: "/api/no-such-endpoint", status: 404)`
- `TicketDesk.Web.Tests.GatewayTests.Migrated_endpoints_win_and_api_paths_are_never_proxied(path: "/api/no-such-endpoint.json", status: 404)`
- `TicketDesk.Web.Tests.GatewayTests.Migrated_endpoints_win_and_api_paths_are_never_proxied(path: "/api/tickets", status: 401)`
- `TicketDesk.Web.Tests.GatewayTests.Migrated_endpoints_win_and_api_paths_are_never_proxied(path: "/health/live", status: 200)`
- `TicketDesk.Web.Tests.GatewayTests.Migrated_endpoints_win_and_api_paths_are_never_proxied(path: "/openapi/v1.json", status: 200)`
- `TicketDesk.Web.Tests.GatewayTests.Missing_antiforgery_cookie_is_rejected_by_the_legacy_app_not_the_gateway`
- `TicketDesk.Web.Tests.GatewayTests.Posting_to_a_get_only_api_endpoint_is_not_proxied`
- `TicketDesk.Web.Tests.GatewayTests.Raw_path_query_cookies_and_ajax_headers_reach_the_legacy_app`
- `TicketDesk.Web.Tests.GatewayTests.Relative_and_foreign_redirects_are_untouched(path: "/redirect/external", location: "https://login.example.test/authorize?client_id=x&r"···)`
- `TicketDesk.Web.Tests.GatewayTests.Relative_and_foreign_redirects_are_untouched(path: "/redirect/relative", location: "/ticket/5?tab=events")`
- `TicketDesk.Web.Tests.GatewayTests.Set_cookie_headers_pass_through_unchanged`
- `TicketDesk.Web.Tests.HealthTests.Legacy_outage_degrades_readiness_without_failing_it`
- `TicketDesk.Web.Tests.HealthTests.Ready_is_healthy_when_database_and_legacy_answer`
- `TicketDesk.Web.Tests.MigrationRoutingTests.A_group_moves_only_as_a_whole`
- `TicketDesk.Web.Tests.MigrationRoutingTests.A_route_switched_back_in_the_registry_file_goes_to_legacy_without_a_restart`
- `TicketDesk.Web.Tests.MigrationRoutingTests.File_posts_move_only_after_the_pages_that_host_the_upload_widget`
- `TicketDesk.Web.Tests.MigrationRoutingTests.Requests_with_the_legacy_sign_in_cookie_stay_on_legacy_until_remote_authentication_is_on`
- `TicketDesk.Web.Tests.MigrationRoutingTests.Status_counts_migrated_routes_per_wave`
- `TicketDesk.Web.Tests.MigrationRoutingTests.Unlisted_routes_and_unknown_bundles_are_proxied`
- `TicketDesk.Web.Tests.MigrationRoutingTests.With_remote_authentication_a_signed_in_request_is_answered_by_the_new_app_after_asking_the_legacy_app`
- `TicketDesk.Web.Tests.MigrationRoutingTests.Without_the_registry_file_nothing_is_migrated`
- `TicketDesk.Web.Tests.RouteInventoryTests.Groups_and_ported_child_rows_name_inventory_rows`
- `TicketDesk.Web.Tests.RouteInventoryTests.Shipped_registry_file_lists_only_known_ids_and_whole_groups`
- `TicketDesk.Web.Tests.SharedSignInTests.Change_password_writes_a_v2_hash_and_edit_profile_saves_L5`
- `TicketDesk.Web.Tests.SharedSignInTests.Register_is_refused_while_disabled_S4`
- `TicketDesk.Web.Tests.SharedSignInTests.Register_when_enabled_creates_a_pending_user_with_an_email_destination_and_a_v2_hash`
- `TicketDesk.Web.Tests.SharedSignInTests.Sign_in_by_email_and_by_user_name_and_lockout_after_five_failures`
- `TicketDesk.Web.Tests.SharedSignInTests.Sign_in_issues_the_legacy_cookie_that_a_second_instance_reads_from_the_shared_key_ring`
- `TicketDesk.Web.Tests.SharedSignInTests.Sign_out_deletes_the_cookie_like_owin_and_ends_the_session`

Added tests:

- `TicketDesk.Database.Tests.ExpandTests.EF_Core_without_HasTrigger_cannot_update_tickets_while_the_expand_trigger_exists`
- `TicketDesk.Identity.Tests.PasswordCompatibilityTests.A_v2_compatibility_hasher_still_verifies_v3_hashes`
- `TicketDesk.Identity.Tests.PasswordCompatibilityTests.Host_configures_IdentityV3_with_the_framework_iteration_count`
- `TicketDesk.Identity.Tests.PasswordCompatibilityTests.Legacy_v2_hash_verifies_and_asks_for_a_rehash`
- `TicketDesk.Identity.Tests.PasswordCompatibilityTests.New_hashes_are_v3_with_sha512_and_the_legacy_algorithm_cannot_verify_them`
- `TicketDesk.Identity.Tests.UserStoreTests.New_and_changed_passwords_are_stored_as_v3_hashes`
- `TicketDesk.Identity.Tests.UserStoreTests.Seeded_v2_hash_signs_in_and_is_rehashed_to_v3`
- `TicketDesk.Web.Tests.AngularPagesTests.Switching_back_gives_the_server_page_without_a_restart`
- `TicketDesk.Web.Tests.HealthTests.Ready_is_healthy_when_the_database_answers_and_checks_nothing_else`
- `TicketDesk.Web.Tests.MigrationRoutingTests.Legacy_or_unknown_modes_in_the_registry_are_ignored_and_the_server_page_answers`
- `TicketDesk.Web.Tests.MigrationRoutingTests.Paths_the_new_app_does_not_have_are_404_and_nothing_is_proxied`
- `TicketDesk.Web.Tests.MigrationRoutingTests.Status_counts_migrated_and_retired_routes_per_wave`
- `TicketDesk.Web.Tests.MigrationRoutingTests.Without_the_registry_file_every_migrated_route_is_answered_by_the_new_app`
- `TicketDesk.Web.Tests.RetiredRoutesTests.Demo_data_actions_answer_410_to_administrators_and_keep_the_legacy_refusals_X2`
- `TicketDesk.Web.Tests.RetiredRoutesTests.First_run_setup_answers_410_with_a_pointer_and_runs_nothing_X1(method: "GET", path: "/first-run-setup", route: "setup.index")`
- `TicketDesk.Web.Tests.RetiredRoutesTests.First_run_setup_answers_410_with_a_pointer_and_runs_nothing_X1(method: "GET", path: "/first-run-setup/check-upgrade-progress", route: "setup.check-upgrade-progress")`
- `TicketDesk.Web.Tests.RetiredRoutesTests.First_run_setup_answers_410_with_a_pointer_and_runs_nothing_X1(method: "GET", path: "/first-run-setup/index", route: "setup.index")`
- `TicketDesk.Web.Tests.RetiredRoutesTests.First_run_setup_answers_410_with_a_pointer_and_runs_nothing_X1(method: "GET", path: "/first-run-setup/legacy-migration", route: "setup.legacy-migration")`
- `TicketDesk.Web.Tests.RetiredRoutesTests.First_run_setup_answers_410_with_a_pointer_and_runs_nothing_X1(method: "GET", path: "/first-run-setup/upgrade-database", route: "setup.upgrade-database")`
- `TicketDesk.Web.Tests.RetiredRoutesTests.First_run_setup_answers_410_with_a_pointer_and_runs_nothing_X1(method: "POST", path: "/first-run-setup", route: "setup.index")`
- `TicketDesk.Web.Tests.RetiredRoutesTests.First_run_setup_answers_410_with_a_pointer_and_runs_nothing_X1(method: "POST", path: "/first-run-setup/create-database", route: "setup.create-database")`
- `TicketDesk.Web.Tests.RetiredRoutesTests.Golden_exchanges_of_retired_routes_get_the_expected_difference(key: "anonymous#009.0", legacy: 302, now: 410)`
- `TicketDesk.Web.Tests.RetiredRoutesTests.Golden_exchanges_of_retired_routes_get_the_expected_difference(key: "anonymous#010.0", legacy: 302, now: 410)`
- `TicketDesk.Web.Tests.RetiredRoutesTests.Golden_exchanges_of_retired_routes_get_the_expected_difference(key: "anonymous#011.0", legacy: 302, now: 410)`
- `TicketDesk.Web.Tests.RetiredRoutesTests.Golden_exchanges_of_retired_routes_get_the_expected_difference(key: "anonymous#012.0", legacy: 302, now: 410)`
- `TicketDesk.Web.Tests.RetiredRoutesTests.Golden_exchanges_of_retired_routes_get_the_expected_difference(key: "anonymous#013.0", legacy: 200, now: 404)`
- `TicketDesk.Web.Tests.RetiredRoutesTests.Status_reports_the_nine_retired_rows_each_with_its_replacement`
- `TicketDesk.Web.Tests.RetiredRoutesTests.The_email_test_page_is_gone_S2`
- `TicketDesk.Web.Tests.RouteInventoryTests.Ported_child_rows_name_inventory_rows`
- `TicketDesk.Web.Tests.RouteInventoryTests.Shipped_registry_file_puts_the_three_Angular_pages_on_Angular_and_nothing_else`
- `TicketDesk.Web.Tests.RouteInventoryTests.Static_files_the_bundles_and_pages_reference_are_served_as_the_legacy_files`
- `TicketDesk.Web.Tests.SignInTests.A_sign_in_cookie_from_before_the_switch_is_not_read_so_the_user_signs_in_once_more`
- `TicketDesk.Web.Tests.SignInTests.A_v2_hash_still_signs_in_and_is_rehashed_to_v3_which_then_signs_in`
- `TicketDesk.Web.Tests.SignInTests.Change_password_writes_a_v3_hash_and_edit_profile_saves_L5`
- `TicketDesk.Web.Tests.SignInTests.Register_is_refused_while_disabled_S4`
- `TicketDesk.Web.Tests.SignInTests.Register_when_enabled_creates_a_pending_user_with_an_email_destination_and_a_v3_hash`
- `TicketDesk.Web.Tests.SignInTests.Sign_in_by_email_and_by_user_name_and_lockout_after_five_failures`
- `TicketDesk.Web.Tests.SignInTests.Sign_in_issues_the_apps_own_cookie_that_a_second_instance_reads_from_the_key_ring_in_the_database`
- `TicketDesk.Web.Tests.SignInTests.Sign_out_deletes_the_cookie_and_ends_the_session`
