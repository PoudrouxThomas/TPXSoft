create table if not exists items (
  id uuid primary key,
  name varchar(200) not null,
  description varchar(2000) not null default '',
  created_at timestamptz not null
);
