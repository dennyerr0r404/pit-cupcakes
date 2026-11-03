-- =====================================================================
-- Dados iniciais (carga mínima para demonstração e testes)
-- Executar após 01_schema.sql
-- =====================================================================
-- Credenciais de demonstração (trocar antes de publicar):
--   Administrador: admin@cupcakes.com   / Admin@123
--   Cliente:       cliente@teste.com    / Cliente@123
-- As senhas estão armazenadas como hash PBKDF2 (formato do PasswordHasher do ASP.NET Core Identity).
-- =====================================================================

INSERT INTO categoria (nome, descricao, ordem_exibicao) VALUES
    ('Tradicionais', 'Sabores clássicos da casa',               1),
    ('Veganos',      'Sem ingredientes de origem animal',        2),
    ('Especiais',    'Receitas sazonais e edições limitadas',    3);

INSERT INTO usuario (nome, email, telefone, senha_hash, perfil) VALUES
    ('Administrador da Loja', 'admin@cupcakes.com', '(11) 99999-0000',
     'AQAAAAIAAYagAAAAEBgZY47kMAOKD3qQYyw5a+PCrAOzB1ZLG08+MCSaIJjUGfcQyy6z77w8GrveVHfcAw==', 'ADMIN'),
    ('Cliente de Teste', 'cliente@teste.com', '(11) 98888-0000',
     'AQAAAAIAAYagAAAAENKR7C4rTizpU5OwkzohcHH/gTO4Tl6VgyvqrbcaBk4FMefcBmppHc6c3tY1SNHgFg==', 'CLIENTE');

INSERT INTO produto (id_categoria, nome, descricao, preco, quantidade_estoque, imagem_url) VALUES
    ((SELECT id_categoria FROM categoria WHERE nome = 'Tradicionais'), 'Velvet Red',
     'Massa fofinha de baunilha com toque de cacau e cobertura de cream cheese.', 12.00, 30, '/img/velvet-red.svg'),
    ((SELECT id_categoria FROM categoria WHERE nome = 'Tradicionais'), 'Chocolate Belga',
     'Massa de chocolate 50% cacau com ganache de chocolate belga.',               12.00, 30, '/img/chocolate-belga.svg'),
    ((SELECT id_categoria FROM categoria WHERE nome = 'Tradicionais'), 'Baunilha Clássico',
     'Massa de baunilha com buttercream suave e confeitos coloridos.',             10.00, 25, '/img/baunilha.svg'),
    ((SELECT id_categoria FROM categoria WHERE nome = 'Veganos'),      'Cenoura Vegana',
     'Massa de cenoura sem ovos e sem leite, com cobertura de cacau e óleo de coco.', 13.00, 15, '/img/cenoura-vegana.svg'),
    ((SELECT id_categoria FROM categoria WHERE nome = 'Veganos'),      'Limão Vegano',
     'Massa de limão siciliano com cobertura de chantilly vegetal.',               13.00, 15, '/img/limao-vegano.svg'),
    ((SELECT id_categoria FROM categoria WHERE nome = 'Especiais'),    'Pistache',
     'Massa de baunilha recheada com creme de pistache e cobertura de pistache.',  16.00, 10, '/img/pistache.svg'),
    ((SELECT id_categoria FROM categoria WHERE nome = 'Especiais'),    'Frutas Vermelhas',
     'Massa de baunilha com geleia artesanal de frutas vermelhas.',                15.00, 10, '/img/frutas-vermelhas.svg');
