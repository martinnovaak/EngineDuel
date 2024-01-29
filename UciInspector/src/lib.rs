use std::ffi::CStr;
use std::os::raw::c_char;
use cozy_chess::{Board, Color, GameStatus, Move, MoveParseError, Piece, Square};

#[repr(C)]
pub enum GameState {
    Ongoing,
    Draw,
    Checkmate,
    Error,
}

#[no_mangle]
pub unsafe extern "C" fn process_moves(moves: *const c_char) -> GameState {
    // Convert the C string to a Rust string
    let c_str = match CStr::from_ptr(moves).to_str().ok() {
        None => return GameState::Error,
        Some(st) => st,
    };

    let mut board = Board::default();

    // Vector of zobrist hash values
    let mut hash_vector: Vec<u64> = Vec::new();
    hash_vector.push(board.hash());

    // Process each move
    for uci_move in c_str.split_whitespace() {
        // transform castling
        let transformed_move = match uci_move {
            "e1g1" if board.piece_on(Square::E1) == Some(Piece::King) => "e1h1",
            "e1c1" if board.piece_on(Square::E1) == Some(Piece::King) => "e1a1",
            "e8g8" if board.piece_on(Square::E8) == Some(Piece::King) => "e8h8",
            "e8c8" if board.piece_on(Square::E8) == Some(Piece::King) => "e8a8",
            _rest => uci_move,
        };

        let parsed_transformed_move: Result<Move, MoveParseError> = transformed_move.parse();

        // Check if parsing was successful
        match parsed_transformed_move {
            Ok(parsed_move) => parsed_move,
            Err(err) => {
                println!("Error parsing move: {:?} {}", err, uci_move);
                println!("{}", c_str);
                return GameState::Error;
            }
        };

        if let Err(_) = board.try_play(parsed_transformed_move.unwrap()) {
            return GameState::Error;
        }

        hash_vector.push(board.hash());
    }

    let white = board.colors(Color::White);
    let black = board.colors(Color::Black);
    let number_of_pieces = white.len() + black.len();

    // check for draw by insufficient material
    if number_of_pieces == 2 {
        return GameState::Draw;
    } else if number_of_pieces == 3 {
        if board.pieces(Piece::Bishop).len() + board.pieces(Piece::Knight).len() >= 1 {
            return GameState::Draw;
        }
    }

    let halfmove_clock = board.halfmove_clock() as usize;

    // Get the final game state
    return match board.status() {
        GameStatus::Won => GameState::Checkmate,
        GameStatus::Drawn => GameState::Draw,
        GameStatus::Ongoing => {
            if halfmove_clock == 0 {
                return GameState::Ongoing;
            }

            // Check for threefold repetition to determine a draw
            let subarray = &hash_vector[hash_vector.len() - halfmove_clock ..];
            let last_element = subarray.last().unwrap();
            let occurrences = subarray.iter().filter(|&&x| x == *last_element).count();
            if occurrences == 3 {
                return GameState::Draw;
            }

            GameState::Ongoing
        },
    }
}
