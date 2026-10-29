using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceScheduler.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Demo reference data with fixed ids so Swagger and Postman calls can be repeated.
    /// Both dealerships work in Asia/Kolkata, 08:00-18:00 every day (local time).
    /// </remarks>
    public partial class SeedDemoData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO capabilities (id, name) VALUES
                    ('66666666-6666-6666-6666-666666666666', 'Lift'),
                    ('67676767-6767-6767-6767-676767676767', 'Diagnostics'),
                    ('68686868-6868-6868-6868-686868686868', 'Alignment rack')
                ON CONFLICT DO NOTHING;

                INSERT INTO skills (id, name) VALUES
                    ('55555555-5555-5555-5555-555555555555', 'Brakes'),
                    ('56565656-5656-5656-5656-565656565656', 'Engine'),
                    ('57575757-5757-5757-5757-575757575757', 'Electrical')
                ON CONFLICT DO NOTHING;

                INSERT INTO customers (id, name, email) VALUES
                    ('33333333-3333-3333-3333-333333333333', 'Demo Customer One', 'demo.customer1@example.com'),
                    ('34343434-3434-3434-3434-343434343434', 'Demo Customer Two', 'demo.customer2@example.com'),
                    ('35353535-3535-3535-3535-353535353535', 'Demo Customer Three', 'demo.customer3@example.com')
                ON CONFLICT DO NOTHING;

                INSERT INTO dealerships (id, name, time_zone_id) VALUES
                    ('11111111-1111-1111-1111-111111111111', 'Demo Dealership Mumbai', 'Asia/Kolkata'),
                    ('12121212-1212-1212-1212-121212121212', 'Demo Dealership Delhi', 'Asia/Kolkata')
                ON CONFLICT DO NOTHING;

                INSERT INTO working_hours (dealership_id, day_of_week, opens_at, closes_at)
                SELECT d.id, w, TIME '08:00', TIME '18:00'
                FROM (VALUES
                    ('11111111-1111-1111-1111-111111111111'::uuid),
                    ('12121212-1212-1212-1212-121212121212'::uuid)) AS d(id)
                CROSS JOIN generate_series(0, 6) AS w
                ON CONFLICT DO NOTHING;

                INSERT INTO service_types (id, name, duration) VALUES
                    ('22222222-2222-2222-2222-222222222222', 'Brake service', INTERVAL '1 hour'),
                    ('23232323-2323-2323-2323-232323232323', 'Engine diagnostic', INTERVAL '2 hours'),
                    ('24242424-2424-2424-2424-242424242424', 'Wheel alignment', INTERVAL '45 minutes')
                ON CONFLICT DO NOTHING;

                INSERT INTO service_type_required_skills (service_type_id, skill_id) VALUES
                    ('22222222-2222-2222-2222-222222222222', '55555555-5555-5555-5555-555555555555'),
                    ('23232323-2323-2323-2323-232323232323', '56565656-5656-5656-5656-565656565656'),
                    ('24242424-2424-2424-2424-242424242424', '55555555-5555-5555-5555-555555555555')
                ON CONFLICT DO NOTHING;

                INSERT INTO service_type_required_capabilities (service_type_id, capability_id) VALUES
                    ('22222222-2222-2222-2222-222222222222', '66666666-6666-6666-6666-666666666666'),
                    ('23232323-2323-2323-2323-232323232323', '67676767-6767-6767-6767-676767676767'),
                    ('24242424-2424-2424-2424-242424242424', '68686868-6868-6868-6868-686868686868')
                ON CONFLICT DO NOTHING;

                INSERT INTO technicians (id, dealership_id, name) VALUES
                    ('77777777-7777-7777-7777-777777777777', '11111111-1111-1111-1111-111111111111', 'Mumbai Technician 1'),
                    ('78787878-7878-7878-7878-787878787878', '11111111-1111-1111-1111-111111111111', 'Mumbai Technician 2'),
                    ('79797979-7979-7979-7979-797979797979', '12121212-1212-1212-1212-121212121212', 'Delhi Technician 1'),
                    ('80808080-8080-8080-8080-808080808080', '12121212-1212-1212-1212-121212121212', 'Delhi Technician 2')
                ON CONFLICT DO NOTHING;

                INSERT INTO technician_skills (technician_id, skill_id) VALUES
                    ('77777777-7777-7777-7777-777777777777', '55555555-5555-5555-5555-555555555555'),
                    ('77777777-7777-7777-7777-777777777777', '56565656-5656-5656-5656-565656565656'),
                    ('78787878-7878-7878-7878-787878787878', '55555555-5555-5555-5555-555555555555'),
                    ('79797979-7979-7979-7979-797979797979', '56565656-5656-5656-5656-565656565656'),
                    ('79797979-7979-7979-7979-797979797979', '57575757-5757-5757-5757-575757575757'),
                    ('80808080-8080-8080-8080-808080808080', '55555555-5555-5555-5555-555555555555')
                ON CONFLICT DO NOTHING;

                INSERT INTO service_bays (id, dealership_id, name) VALUES
                    ('99999999-9999-9999-9999-999999999999', '11111111-1111-1111-1111-111111111111', 'Mumbai Bay 1'),
                    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111', 'Mumbai Bay 2'),
                    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '12121212-1212-1212-1212-121212121212', 'Delhi Bay 1'),
                    ('cccccccc-cccc-cccc-cccc-cccccccccccc', '12121212-1212-1212-1212-121212121212', 'Delhi Bay 2')
                ON CONFLICT DO NOTHING;

                INSERT INTO service_bay_capabilities (service_bay_id, capability_id) VALUES
                    ('99999999-9999-9999-9999-999999999999', '66666666-6666-6666-6666-666666666666'),
                    ('99999999-9999-9999-9999-999999999999', '67676767-6767-6767-6767-676767676767'),
                    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '66666666-6666-6666-6666-666666666666'),
                    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '68686868-6868-6868-6868-686868686868'),
                    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '66666666-6666-6666-6666-666666666666'),
                    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '67676767-6767-6767-6767-676767676767'),
                    ('cccccccc-cccc-cccc-cccc-cccccccccccc', '66666666-6666-6666-6666-666666666666'),
                    ('cccccccc-cccc-cccc-cccc-cccccccccccc', '68686868-6868-6868-6868-686868686868')
                ON CONFLICT DO NOTHING;

                INSERT INTO vehicles (id, customer_id, vin, make, model) VALUES
                    ('44444444-4444-4444-4444-444444444444', '33333333-3333-3333-3333-333333333333', '1HGCM82633A004352', 'Honda', 'Civic'),
                    ('45454545-4545-4545-4545-454545454545', '34343434-3434-3434-3434-343434343434', '2T1BURHE0JC034941', 'Toyota', 'Corolla'),
                    ('46464646-4646-4646-4646-464646464646', '35353535-3535-3535-3535-353535353535', 'WVWZZZ1JZXW000001', 'Volkswagen', 'Polo')
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deletes only the demo rows. Appointments that reference them must be removed first.
            migrationBuilder.Sql("""
                DELETE FROM vehicles WHERE id IN ('44444444-4444-4444-4444-444444444444', '45454545-4545-4545-4545-454545454545', '46464646-4646-4646-4646-464646464646');
                DELETE FROM service_bay_capabilities WHERE service_bay_id IN ('99999999-9999-9999-9999-999999999999', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'cccccccc-cccc-cccc-cccc-cccccccccccc');
                DELETE FROM service_bays WHERE id IN ('99999999-9999-9999-9999-999999999999', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'cccccccc-cccc-cccc-cccc-cccccccccccc');
                DELETE FROM technician_skills WHERE technician_id IN ('77777777-7777-7777-7777-777777777777', '78787878-7878-7878-7878-787878787878', '79797979-7979-7979-7979-797979797979', '80808080-8080-8080-8080-808080808080');
                DELETE FROM technicians WHERE id IN ('77777777-7777-7777-7777-777777777777', '78787878-7878-7878-7878-787878787878', '79797979-7979-7979-7979-797979797979', '80808080-8080-8080-8080-808080808080');
                DELETE FROM service_type_required_capabilities WHERE service_type_id IN ('22222222-2222-2222-2222-222222222222', '23232323-2323-2323-2323-232323232323', '24242424-2424-2424-2424-242424242424');
                DELETE FROM service_type_required_skills WHERE service_type_id IN ('22222222-2222-2222-2222-222222222222', '23232323-2323-2323-2323-232323232323', '24242424-2424-2424-2424-242424242424');
                DELETE FROM service_types WHERE id IN ('22222222-2222-2222-2222-222222222222', '23232323-2323-2323-2323-232323232323', '24242424-2424-2424-2424-242424242424');
                DELETE FROM working_hours WHERE dealership_id IN ('11111111-1111-1111-1111-111111111111', '12121212-1212-1212-1212-121212121212');
                DELETE FROM dealerships WHERE id IN ('11111111-1111-1111-1111-111111111111', '12121212-1212-1212-1212-121212121212');
                DELETE FROM customers WHERE id IN ('33333333-3333-3333-3333-333333333333', '34343434-3434-3434-3434-343434343434', '35353535-3535-3535-3535-353535353535');
                DELETE FROM skills WHERE id IN ('55555555-5555-5555-5555-555555555555', '56565656-5656-5656-5656-565656565656', '57575757-5757-5757-5757-575757575757');
                DELETE FROM capabilities WHERE id IN ('66666666-6666-6666-6666-666666666666', '67676767-6767-6767-6767-676767676767', '68686868-6868-6868-6868-686868686868');
                """);
        }
    }
}
